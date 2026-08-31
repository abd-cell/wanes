using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Notifications;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Notifications.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Notifications.Fcm;
using Wanes.Shareds.Security;
using Wanes.Shareds.SSE;

namespace Wanes.Areas.Services.Notifications;

public class NotificationService : INotificationService
{
    /// <summary>Newest-first page size for the in-app inbox.</summary>
    private const int FeedSize = 50;

    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IFcmSender fcmSender;
    private readonly SseConnectionManager sseConnectionManager;
    private readonly ILogger<NotificationService> logger;
    private readonly IRepository<UserNotification> notificationRepository;
    private readonly IRepository<UserLogin> userLoginRepository;
    private readonly IRepository<User> userRepository;

    public NotificationService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IFcmSender fcmSender,
        SseConnectionManager sseConnectionManager,
        ILogger<NotificationService> logger,
        IRepository<UserNotification> notificationRepository,
        IRepository<UserLogin> userLoginRepository,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.fcmSender = fcmSender;
        this.sseConnectionManager = sseConnectionManager;
        this.logger = logger;
        this.notificationRepository = notificationRepository;
        this.userLoginRepository = userLoginRepository;
        this.userRepository = userRepository;
    }

    public Task Notify(int userId, NotificationTemplate template, object? args = null, object? data = null)
        => NotifyRaw(userId, NotificationTexts.TypeOf(template), NotificationTexts.Render(template, args),
            data == null ? null : JsonSerializer.Serialize(data));

    public async Task NotifyRaw(int userId, NotificationType type, LocalizedText text, string? dataJson)
    {
        var notification = new UserNotification
        {
            UserId = userId,
            Type = type,
            Title = text.Title,
            Body = text.Body,
            TitleAr = text.TitleAr,
            BodyAr = text.BodyAr,
            DataJson = dataJson,
        };
        notificationRepository.Create(notification);
        await unitOfWork.SaveAsync();

        await Deliver(notification);
    }

    public async Task NotifyMany(IEnumerable<int> userIds, NotificationTemplate template, object? args = null,
        object? data = null)
    {
        var recipients = userIds.Distinct().ToList();
        if (recipients.Count == 0) return;

        var text = NotificationTexts.Render(template, args);
        var type = NotificationTexts.TypeOf(template);
        var dataJson = data == null ? null : JsonSerializer.Serialize(data);

        // One row per recipient carrying both languages. Which one each device is
        // pushed is decided in Deliver, from that recipient's own preference.
        var notifications = recipients.Select(userId => new UserNotification
        {
            UserId = userId,
            Type = type,
            Title = text.Title,
            Body = text.Body,
            TitleAr = text.TitleAr,
            BodyAr = text.BodyAr,
            DataJson = dataJson,
        }).ToList();

        foreach (var notification in notifications) notificationRepository.Create(notification);
        await unitOfWork.SaveAsync();

        foreach (var notification in notifications) await Deliver(notification);
    }

    /// <summary>
    /// Push + SSE fan-out for an already-persisted notification. Swallows all
    /// failures: the stored row is the source of truth, delivery is best-effort
    /// and must never fail the booking or trip action that triggered it.
    /// </summary>
    private async Task Deliver(UserNotification notification)
    {
        try
        {
            // Both settings in one round trip: whether to push at all, and which
            // language to push in.
            var reader = await userRepository
                .Where(u => u.Id == notification.UserId)
                .Select(u => new { u.NotifPush, u.Language })
                .FirstOrDefaultAsync();

            // NotifPush gates the *push* only. The row is already stored and the
            // SSE copy still goes out, so opting out of push does not cost the
            // user the in-app inbox.
            if (reader is { NotifPush: true })
            {
                var tokens = await userLoginRepository
                    .Where(l => l.UserId == notification.UserId && l.DeviceToken != null && !l.IsDeleted)
                    .Select(l => l.DeviceToken!)
                    .ToListAsync();

                // A push payload holds one title and one body, so unlike the SSE
                // copy it has to commit to a language here.
                var (title, body) = TextOf(notification).For(reader.Language);

                var result = await fcmSender.SendAsync(tokens, title, body, BuildPushData(notification));

                if (result.InvalidTokens.Count > 0) await PruneTokens(result.InvalidTokens);
            }

            await Stream(notification);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Delivering notification {Id} to user {UserId} failed.",
                notification.Id, notification.UserId);
        }
    }

    /// <summary>
    /// The in-app half of delivery: pushes one notification down the recipient's
    /// SSE stream. <paramref name="dedupeKey"/> is set for broadcasts, whose push
    /// copy carries no per-user row id -- see <see cref="BuildBroadcastPushData"/>.
    /// </summary>
    private async Task Stream(UserNotification notification, string? dedupeKey = null)
    {
        var payload = JsonSerializer.Serialize(new
        {
            id = notification.Id,
            type = notification.Type.ToString(),
            title = notification.Title,
            body = notification.Body,
            // JsonNode re-emits the stored payload as real JSON rather than
            // a quoted string, so SSE and push agree on the shape.
            titleAr = notification.TitleAr,
            bodyAr = notification.BodyAr,
            data = ParsePayload(notification.DataJson),
            dedupe = dedupeKey,
        });
        await sseConnectionManager.SendAsync(notification.UserId, payload);
    }

    /// <summary>The stored row's wording, back in the shape that can pick a language.</summary>
    private static LocalizedText TextOf(UserNotification notification) =>
        LocalizedText.Raw(notification.Title, notification.Body, notification.TitleAr, notification.BodyAr);

    /// <summary>
    /// FCM data values must all be strings, so the caller's payload rides along
    /// as raw JSON under "data" and the app re-parses it to deep-link the tap.
    /// </summary>
    private static Dictionary<string, string> BuildPushData(UserNotification notification)
    {
        var push = new Dictionary<string, string>
        {
            ["notificationId"] = notification.Id.ToString(),
            ["type"] = notification.Type.ToString(),
            // Required for a tap to reach the Flutter handler on Android.
            ["click_action"] = "FLUTTER_NOTIFICATION_CLICK",
        };
        if (!string.IsNullOrEmpty(notification.DataJson)) push["data"] = notification.DataJson;
        return push;
    }

    /// <summary>Stored payloads come from us, but an admin can hand-edit one — bad JSON is dropped, not thrown.</summary>
    private static JsonNode? ParsePayload(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson)) return null;
        try { return JsonNode.Parse(dataJson); }
        catch (JsonException) { return null; }
    }

    /// <summary>Drops device tokens Firebase reported as dead, so they aren't retried forever.</summary>
    private async Task PruneTokens(IReadOnlyList<string> tokens)
    {
        var stale = await userLoginRepository
            .Where(l => l.DeviceToken != null && tokens.Contains(l.DeviceToken))
            .ToListAsync();
        if (stale.Count == 0) return;

        foreach (var login in stale)
        {
            login.DeviceToken = null;
            userLoginRepository.Update(login);
        }
        await unitOfWork.SaveAsync();
    }

    public async Task<int> NotifyAudience(NotificationAudience audience, NotificationType type,
        LocalizedText text, object? data = null)
    {
        var recipients = await AudienceQuery(audience).Select(u => u.Id).ToListAsync();
        if (recipients.Count == 0) return 0;

        var dataJson = data == null ? null : JsonSerializer.Serialize(data);

        var notifications = recipients.Select(userId => new UserNotification
        {
            UserId = userId,
            Type = type,
            Title = text.Title,
            Body = text.Body,
            TitleAr = text.TitleAr,
            BodyAr = text.BodyAr,
            DataJson = dataJson,
        }).ToList();

        foreach (var notification in notifications) notificationRepository.Create(notification);
        await unitOfWork.SaveAsync();

        await DeliverBroadcast(audience.ToString(), AudienceQuery(audience), notifications, type, text, dataJson);
        return recipients.Count;
    }

    public async Task<int> NotifyUsersRaw(IEnumerable<int> userIds, NotificationType type, LocalizedText text,
        string? dataJson)
    {
        var requested = userIds.Distinct().ToList();
        if (requested.Count == 0) return 0;

        // Filtered through the same rule the audiences use rather than trusting
        // the ids as given: an admin picking names off a list should not be able
        // to write inbox rows for accounts that have since been disabled.
        // The list is hand-picked and therefore small, so Contains is safe here
        // in a way it would not be for a whole audience.
        var recipientQuery = userRepository.Where(u => !u.IsDisabled && requested.Contains(u.Id));
        var recipients = await recipientQuery.Select(u => u.Id).ToListAsync();
        if (recipients.Count == 0) return 0;

        var notifications = recipients.Select(userId => new UserNotification
        {
            UserId = userId,
            Type = type,
            Title = text.Title,
            Body = text.Body,
            TitleAr = text.TitleAr,
            BodyAr = text.BodyAr,
            DataJson = dataJson,
        }).ToList();

        foreach (var notification in notifications) notificationRepository.Create(notification);
        await unitOfWork.SaveAsync();

        await DeliverBroadcast($"{recipients.Count} picked user(s)", recipientQuery, notifications, type, text,
            dataJson);
        return recipients.Count;
    }

    /// <summary>
    /// Delivery for a whole audience. Differs from <see cref="Deliver"/> in the
    /// one way that matters at scale: every device in the audience is pushed with
    /// a *single* multicast (the sender chunks it at 500), so a broadcast to 10k
    /// users is one round trip to Google rather than 10k. SSE stays
    /// per-connection either way, but that is an in-memory write.
    /// </summary>
    private async Task DeliverBroadcast(string audienceLabel, IQueryable<User> audienceQuery,
        List<UserNotification> notifications, NotificationType type, LocalizedText text, string? dataJson)
    {
        // One shared key across both transports: the push copy has no per-user
        // row id, so without this a foregrounded app counts the FCM and the SSE
        // copy of the same broadcast twice.
        var dedupeKey = Guid.NewGuid().ToString("N");

        try
        {
            // Left as IQueryable so EF emits a subquery -- passing thousands of
            // ids as parameters would blow SQL Server's 2100-parameter ceiling.
            var pushEnabled = audienceQuery.Where(u => u.NotifPush);

            // Each device carries its owner's language so the fan-out can stay a
            // multicast: one send per language present, not one per recipient.
            var devices = await userLoginRepository
                .Where(l => l.DeviceToken != null && !l.IsDeleted)
                .Join(pushEnabled, l => l.UserId, u => u.Id,
                    (l, u) => new { Token = l.DeviceToken!, u.Language })
                .ToListAsync();

            var pushed = 0;
            foreach (var group in devices.GroupBy(d => d.Language))
            {
                var (title, body) = text.For(group.Key);
                var result = await fcmSender.SendAsync(group.Select(d => d.Token), title, body,
                    BuildBroadcastPushData(type, dataJson, dedupeKey));

                if (result.InvalidTokens.Count > 0) await PruneTokens(result.InvalidTokens);
                pushed += result.SuccessCount;
            }

            logger.LogInformation(
                "Broadcast to {Audience}: {Rows} inbox row(s), {Devices} device(s) in {Langs} language(s), {Sent} pushed.",
                audienceLabel, notifications.Count, devices.Count, devices.Select(d => d.Language).Distinct().Count(),
                pushed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pushing broadcast to {Audience} failed.", audienceLabel);
        }

        foreach (var notification in notifications)
        {
            try
            {
                await Stream(notification, dedupeKey);
            }
            catch (Exception ex)
            {
                // One dead SSE connection must not cost the rest of the audience.
                logger.LogError(ex, "Streaming broadcast to user {UserId} failed.", notification.UserId);
            }
        }
    }

    /// <summary>
    /// The recipients behind a <see cref="NotificationAudience"/>. Disabled
    /// accounts are never included -- they cannot act on a notification.
    /// </summary>
    private IQueryable<User> AudienceQuery(NotificationAudience audience)
    {
        var query = userRepository.Where(u => !u.IsDisabled);
        return audience switch
        {
            NotificationAudience.Riders => query.Where(u => u.IsRider),
            NotificationAudience.Drivers => query.Where(u => u.IsDriver),
            NotificationAudience.VerifiedDrivers =>
                query.Where(u => u.IsDriver && u.DriverStatus == DriverStatus.Verified),
            _ => query,
        };
    }

    /// <summary>
    /// Push payload for a broadcast. Deliberately carries no <c>notificationId</c>:
    /// one multicast serves the whole audience and each recipient's row id
    /// differs. The app already treats a missing id as "not dedupable" and falls
    /// back to <c>dedupe</c>, so the tap still routes and the badge stays right.
    /// </summary>
    private static Dictionary<string, string> BuildBroadcastPushData(NotificationType type, string? dataJson,
        string dedupeKey)
    {
        var push = new Dictionary<string, string>
        {
            ["type"] = type.ToString(),
            ["dedupe"] = dedupeKey,
            ["click_action"] = "FLUTTER_NOTIFICATION_CLICK",
        };
        if (!string.IsNullOrEmpty(dataJson)) push["data"] = dataJson;
        return push;
    }

    public async Task<int> NotifyNearbyDrivers(RideRequest request)
    {
        var driverIds = await userRepository
            .Where(u => u.IsDriver
                        && u.DriverStatus == DriverStatus.Verified
                        && u.IsOnline
                        && u.LastLocation != null
                        && u.Id != request.RiderId
                        && u.LastLocation!.IsWithinDistance(request.Origin, request.RadiusMeters))
            .Select(u => u.Id)
            .ToListAsync();

        await NotifyMany(driverIds, NotificationTemplate.RideRequestNearbyDriver,
            args: new { origin = request.OriginAddress, destination = request.DestinationAddress },
            data: new { requestId = request.Id, seats = request.Seats });

        return driverIds.Count;
    }

    public async Task<BaseResponse<NotificationFeed>> GetUserNotifications()
    {
        var userId = securityManager.RequireUserId();

        var notifications = await notificationRepository
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.Id).Take(FeedSize).ToListAsync();

        var unread = await notificationRepository
            .Where(n => n.UserId == userId && !n.IsRead)
            .CountAsync();

        return new BaseResponse<NotificationFeed>(new NotificationFeed
        {
            Items = notifications.Select(n => new NotificationRow(n)).ToList(),
            UnreadCount = unread,
        });
    }

    public async Task<BaseResponse> MarkRead(int id)
    {
        var userId = securityManager.RequireUserId();
        var notification = notificationRepository.FirstOrDefault(x => x.Id == id && x.UserId == userId);
        if (notification == null) return new BaseResponse(ErrorCode.NotFound);

        notification.IsRead = true;
        notificationRepository.Update(notification);
        await unitOfWork.SaveAsync();
        return new BaseResponse();
    }

    public async Task<BaseResponse> MarkAllRead()
    {
        var userId = securityManager.RequireUserId();
        var unread = await notificationRepository
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notificationRepository.Update(notification);
        }
        await unitOfWork.SaveAsync();
        return new BaseResponse();
    }

    public async Task<BaseResponse> RegisterDevice(RegisterDeviceInput input)
    {
        var sessionKey = securityManager.SessionKey;
        if (string.IsNullOrEmpty(sessionKey)) return new BaseResponse(ErrorCode.Unauthorized);

        var login = userLoginRepository.FirstOrDefault(l => l.SessionKey == sessionKey);
        if (login == null) return new BaseResponse(ErrorCode.NotFound);

        var token = input.DeviceToken.Trim();

        // One handset can carry stale sessions (reinstall, or another account
        // signed in earlier). Leaving the token on those rows would push this
        // user's notifications to whoever else still claims the same device.
        var duplicates = await userLoginRepository
            .Where(l => l.DeviceToken == token && l.Id != login.Id)
            .ToListAsync();
        foreach (var other in duplicates)
        {
            other.DeviceToken = null;
            userLoginRepository.Update(other);
        }

        login.DeviceToken = token;
        if (input.DeviceType != null) login.DeviceType = input.DeviceType.Value;
        login.LastActivityAt = DateTime.UtcNow;
        userLoginRepository.Update(login);

        await unitOfWork.SaveAsync();
        return new BaseResponse();
    }

    public async Task<BaseResponse> ClearDevice()
    {
        var sessionKey = securityManager.SessionKey;
        if (string.IsNullOrEmpty(sessionKey)) return new BaseResponse(ErrorCode.Unauthorized);

        var login = userLoginRepository.FirstOrDefault(l => l.SessionKey == sessionKey);
        if (login == null) return new BaseResponse(ErrorCode.NotFound);

        login.DeviceToken = null;
        userLoginRepository.Update(login);
        await unitOfWork.SaveAsync();
        return new BaseResponse();
    }
}
