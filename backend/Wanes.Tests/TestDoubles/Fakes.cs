using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Configuration.Models;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Notifications.Models;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Notifications.Fcm;
using Wanes.Shareds.Notifications.Sms;
using Wanes.Shareds.Security;
using Wanes.Shareds.Security.Token;

namespace Wanes.Tests.TestDoubles;

public class FakeSecurityManager : ISecurityManager
{
    public FakeSecurityManager(int? userId = 1) => UserId = userId;

    public int? UserId { get; set; }
    public int RequireUserId() => UserId ?? throw new AppException(ErrorCode.Unauthorized);
    public IReadOnlyCollection<Roles> Roles { get; set; } = [Wanes.Shareds.Enums.Roles.User];
    public bool IsInRole(Roles role) => Roles.Contains(role);
    public string? SessionKey { get; set; } = "test-session";
}

public class FakeAuditService : IAuditService
{
    public List<string> Actions { get; } = [];

    public Task LogAsync(string action, string? entityType = null, int? entityId = null,
        object? before = null, object? after = null)
    {
        Actions.Add(action);
        return Task.CompletedTask;
    }
}

public class FakeNotificationService : INotificationService
{
    /// <summary>"userId:template" per delivery, in order, so a test can assert who was told what.</summary>
    public List<string> Sent { get; } = [];
    public int NearbyDriverCount { get; set; } = 2;

    public Task Notify(int userId, NotificationTemplate template, object? args = null, object? data = null)
    {
        Sent.Add($"{userId}:{template}");
        return Task.CompletedTask;
    }

    public Task NotifyRaw(int userId, NotificationType type, LocalizedText text, string? dataJson)
    {
        Sent.Add($"{userId}:{type}");
        return Task.CompletedTask;
    }

    public Task NotifyMany(IEnumerable<int> userIds, NotificationTemplate template, object? args = null,
        object? data = null)
    {
        foreach (var userId in userIds.Distinct()) Sent.Add($"{userId}:{template}");
        return Task.CompletedTask;
    }

    public Task<int> NotifyAudience(NotificationAudience audience, NotificationType type, LocalizedText text,
        object? data = null)
    {
        Sent.Add($"{audience}:{type}");
        return Task.FromResult(0);
    }

    public Task<int> NotifyUsersRaw(IEnumerable<int> userIds, NotificationType type, LocalizedText text,
        string? dataJson)
    {
        var ids = userIds.Distinct().ToList();
        foreach (var userId in ids) Sent.Add($"{userId}:{type}");
        return Task.FromResult(ids.Count);
    }

    public Task<int> NotifyNearbyDrivers(RideRequest request) =>
        Task.FromResult(NearbyDriverCount);

    /// <summary>
    /// Records that the reverse match was asked for, and for which trip. Which
    /// riders it would actually reach is the real service's business — see
    /// <c>ReverseMatchTests</c> — so the fake only pins whether the caller asked.
    /// </summary>
    public Task NotifyWaitingRiders(Trip trip, User driver)
    {
        Sent.Add($"waiting:{trip.Id}");
        return Task.CompletedTask;
    }

    public Task NotifyRideRequestClosed(int requestId, RideRequestStatus reason)
    {
        Sent.Add($"request-{requestId}:{reason}");
        return Task.CompletedTask;
    }

    public Task<BaseResponse<NotificationFeed>> GetUserNotifications() =>
        Task.FromResult(new BaseResponse<NotificationFeed>(new NotificationFeed()));

    public Task<BaseResponse> MarkRead(int id) => Task.FromResult(new BaseResponse());
    public Task<BaseResponse> MarkAllRead() => Task.FromResult(new BaseResponse());
    public Task<BaseResponse> RegisterDevice(RegisterDeviceInput input) => Task.FromResult(new BaseResponse());
    public Task<BaseResponse> ClearDevice() => Task.FromResult(new BaseResponse());
}

/// <summary>The shipped defaults, with the hail TTL a test can move.</summary>
public class FakeAppConfigurationService : IAppConfigurationService
{
    public int HailRequestTtlMinutes { get; set; } = MatchRules.DefaultHailTtlMinutes;

    public Task<BaseResponse<AppConfigurationOutput>> Get() =>
        Task.FromResult(new BaseResponse<AppConfigurationOutput>(new AppConfigurationOutput
        {
            HailRequestTtlMinutes = HailRequestTtlMinutes,
        }));

    public Task<BaseResponse<AppConfigurationOutput>> Update(AppConfigurationInput input) =>
        Task.FromResult(new BaseResponse<AppConfigurationOutput>(new AppConfigurationOutput
        {
            HailRequestTtlMinutes = input.HailRequestTtlMinutes,
        }));
}

public class FakeSmsSender : ISmsSender
{
    public List<string> Sent { get; } = [];

    public Task SendAsync(string phone, string message)
    {
        Sent.Add($"{phone}:{message}");
        return Task.CompletedTask;
    }
}

public class FakeTokenGenerator : ITokenGenerator
{
    public (string Token, DateTime ExpiresAt) Generate(int userId, IEnumerable<Roles> roles, string sessionKey) =>
        ($"token-{userId}-{sessionKey}", DateTime.UtcNow.AddMinutes(15));

    // Deterministic and reversible-by-eye: the hash is the raw value with a marker,
    // so a test can assert the stored hash matches the token it handed out.
    public (string Token, string Hash, DateTime ExpiresAt) GenerateRefreshToken()
    {
        var token = $"refresh-{Guid.NewGuid():N}";
        return (token, HashRefreshToken(token), DateTime.UtcNow.AddDays(30));
    }

    public string HashRefreshToken(string token) => $"hash:{token}";
}

/// <summary>Push turned off, the way a machine with no Firebase credentials runs.</summary>
public class FakeFcmSender : IFcmSender
{
    public bool IsConfigured => false;

    public Task<FcmSendResult> SendAsync(IEnumerable<string> deviceTokens, string title, string body,
        IDictionary<string, string>? data = null) =>
        Task.FromResult(new FcmSendResult());
}
