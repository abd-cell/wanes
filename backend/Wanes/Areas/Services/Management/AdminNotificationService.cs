using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Notifications;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.Areas.Services.Notifications;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;

namespace Wanes.Areas.Services.Management;

public class AdminNotificationService : IAdminNotificationService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IRepository<UserNotification> notificationRepository;
    private readonly IRepository<User> userRepository;

    public AdminNotificationService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        INotificationService notificationService,
        IRepository<UserNotification> notificationRepository,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.notificationRepository = notificationRepository;
        this.userRepository = userRepository;
    }

    public async Task<BaseResponse<PageOutput<NotificationRow>>> List(PageInput page, NotificationType? type, int? userId, bool? isRead)
    {
        var query = notificationRepository.Query();

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(n => n.Title.Contains(term) || n.Body.Contains(term));
        }
        if (type != null) query = query.Where(n => n.Type == type);
        if (userId != null) query = query.Where(n => n.UserId == userId);
        if (isRead != null) query = query.Where(n => n.IsRead == isRead);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(n => n.Id).Paginate(page).ToListAsync();

        var rows = await BuildRows(items);

        return new BaseResponse<PageOutput<NotificationRow>>(new PageOutput<NotificationRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<NotificationRow>> Get(int id)
    {
        var notification = await notificationRepository.GetByIdAsync(id);
        if (notification == null) return new BaseResponse<NotificationRow>(default, ErrorCode.NotFound);
        return new BaseResponse<NotificationRow>((await BuildRows([notification])).First());
    }

    public async Task<BaseResponse<NotificationRow>> Create(NotificationInput input)
    {
        // Goes through the notification service rather than straight to the
        // repository, so an admin-composed notification is pushed and streamed
        // like any other instead of silently sitting in the table.
        await notificationService.NotifyRaw(input.UserId, input.Type,
            LocalizedText.Raw(input.Title, input.Body, input.TitleAr, input.BodyAr), input.DataJson);

        var notification = await notificationRepository
            .Where(n => n.UserId == input.UserId)
            .OrderByDescending(n => n.Id)
            .FirstOrDefaultAsync();
        if (notification == null) return new BaseResponse<NotificationRow>(default, ErrorCode.NotFound);

        // The service always creates it unread; honour an admin who ticked "read".
        if (input.IsRead)
        {
            notification.IsRead = true;
            notificationRepository.Update(notification);
            await unitOfWork.SaveAsync();
        }

        await auditService.LogAsync("admin.notifications.create", nameof(UserNotification), notification.Id);
        return await Get(notification.Id);
    }

    public async Task<BaseResponse<BroadcastResult>> Broadcast(BroadcastInput input)
    {
        var title = input.Title.Trim();
        var body = input.Body.Trim();
        if (title.Length == 0)
            return new BaseResponse<BroadcastResult>(default, ErrorCode.ValidationError, "Title is required.");

        // The admin hands us serialized JSON; NotifyAudience serializes whatever
        // object it is given, so parse first and let a malformed payload go out
        // as no payload rather than as a quoted string.
        if (!TryParseData(input.DataJson, out var data))
            return new BaseResponse<BroadcastResult>(default, ErrorCode.ValidationError,
                "DataJson is not valid JSON.");

        var recipients = await notificationService.NotifyAudience(input.Audience, input.Type,
            LocalizedText.Raw(title, body, Trimmed(input.TitleAr), Trimmed(input.BodyAr)), data);

        // No entity id: a broadcast is many rows, not one. The audience and count
        // are what an auditor needs, so they ride in the "after" payload.
        await auditService.LogAsync("admin.notifications.broadcast", nameof(UserNotification),
            after: new { audience = input.Audience.ToString(), recipients, title });

        return new BaseResponse<BroadcastResult>(new BroadcastResult
        {
            Audience = input.Audience,
            Recipients = recipients,
        });
    }

    public async Task<BaseResponse<TargetedSendResult>> SendTargeted(TargetedSendInput input)
    {
        var title = input.Title.Trim();
        var body = input.Body.Trim();
        if (title.Length == 0)
            return new BaseResponse<TargetedSendResult>(default, ErrorCode.ValidationError, "Title is required.");

        var userIds = input.UserIds.Where(id => id > 0).Distinct().ToList();
        if (userIds.Count == 0)
            return new BaseResponse<TargetedSendResult>(default, ErrorCode.ValidationError,
                "At least one recipient is required.");

        if (!TryParseData(input.DataJson, out _))
            return new BaseResponse<TargetedSendResult>(default, ErrorCode.ValidationError,
                "DataJson is not valid JSON.");

        var recipients = await notificationService.NotifyUsersRaw(userIds, input.Type,
            LocalizedText.Raw(title, body, Trimmed(input.TitleAr), Trimmed(input.BodyAr)),
            Trimmed(input.DataJson));

        // Like a broadcast this is many rows rather than one, so the recipients
        // ride in the payload instead of an entity id. Both the asked-for and the
        // reached count are logged: they differ when an account was disabled.
        await auditService.LogAsync("admin.notifications.send", nameof(UserNotification),
            after: new { requested = userIds.Count, recipients, title });

        return new BaseResponse<TargetedSendResult>(new TargetedSendResult { Recipients = recipients });
    }

    public async Task<BaseResponse<BulkNotificationResult>> Bulk(BulkNotificationInput input)
    {
        var ids = input.Ids.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
            return new BaseResponse<BulkNotificationResult>(default, ErrorCode.ValidationError,
                "At least one notification is required.");

        if (!Enum.IsDefined(input.Action))
            return new BaseResponse<BulkNotificationResult>(default, ErrorCode.ValidationError,
                "Unknown bulk action.");

        var items = await notificationRepository.Where(n => ids.Contains(n.Id)).ToListAsync();

        foreach (var item in items)
        {
            switch (input.Action)
            {
                case NotificationBulkAction.MarkRead:
                    item.IsRead = true;
                    notificationRepository.Update(item);
                    break;
                case NotificationBulkAction.MarkUnread:
                    item.IsRead = false;
                    notificationRepository.Update(item);
                    break;
                case NotificationBulkAction.Delete:
                    notificationRepository.SoftDelete(item);
                    break;
            }
        }

        await unitOfWork.SaveAsync();

        // Ids that matched nothing are dropped silently, so the count reported
        // back is what actually changed rather than what was asked for.
        await auditService.LogAsync($"admin.notifications.bulk.{input.Action}", nameof(UserNotification),
            after: new { requested = ids.Count, affected = items.Count });

        return new BaseResponse<BulkNotificationResult>(new BulkNotificationResult
        {
            Action = input.Action,
            Affected = items.Count,
        });
    }

    public async Task<BaseResponse<NotificationStats>> Stats()
    {
        var query = notificationRepository.Query();
        var since = DateTime.UtcNow.AddHours(-24);

        var total = await query.CountAsync();
        var unread = await query.CountAsync(n => !n.IsRead);

        var byType = await query
            .GroupBy(n => n.Type)
            .Select(g => new NotificationTypeCount { Type = g.Key, Count = g.Count() })
            .OrderByDescending(t => t.Count)
            .ToListAsync();

        return new BaseResponse<NotificationStats>(new NotificationStats
        {
            Total = total,
            Unread = unread,
            Read = total - unread,
            Last24Hours = await query.CountAsync(n => n.CreationDate >= since),
            Recipients = await query.Select(n => n.UserId).Distinct().CountAsync(),
            ByType = byType,
        });
    }

    public async Task<BaseResponse<NotificationRow>> Update(int id, NotificationInput input)
    {
        var notification = await notificationRepository.GetByIdAsync(id);
        if (notification == null) return new BaseResponse<NotificationRow>(default, ErrorCode.NotFound);

        Apply(notification, input);
        notificationRepository.Update(notification);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.notifications.update", nameof(UserNotification), notification.Id);
        return await Get(notification.Id);
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var notification = await notificationRepository.GetByIdAsync(id);
        if (notification == null) return new BaseResponse(ErrorCode.NotFound);

        notificationRepository.SoftDelete(notification);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.notifications.delete", nameof(UserNotification), id);
        return new BaseResponse();
    }

    /// <summary>Attaches the recipient name; UserId has no navigation property.</summary>
    private async Task<List<NotificationRow>> BuildRows(IReadOnlyCollection<UserNotification> items)
    {
        var names = await userRepository.ResolveNames(items.Select(n => (int?)n.UserId));
        return items.Select(n => new NotificationRow(n) { UserName = names.NameFor(n.UserId) }).ToList();
    }

    private static void Apply(UserNotification notification, NotificationInput input)
    {
        notification.UserId = input.UserId;
        notification.Type = input.Type;
        notification.Title = input.Title;
        notification.Body = input.Body;
        notification.TitleAr = Trimmed(input.TitleAr);
        notification.BodyAr = Trimmed(input.BodyAr);
        notification.DataJson = input.DataJson;
        notification.IsRead = input.IsRead;
    }

    /// <summary>
    /// Validates an admin-typed payload. Returns false for malformed JSON so the
    /// caller can reject rather than store text that would silently be dropped
    /// on the way out to the device.
    /// </summary>
    private static bool TryParseData(string? dataJson, out JsonNode? data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(dataJson)) return true;
        try
        {
            data = JsonNode.Parse(dataJson);
            return true;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>Blank Arabic fields are stored as null, so the fallback triggers cleanly.</summary>
    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
