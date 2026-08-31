using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Notifications.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
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

    public Task<BaseResponse<NotificationFeed>> GetUserNotifications() =>
        Task.FromResult(new BaseResponse<NotificationFeed>(new NotificationFeed()));

    public Task<BaseResponse> MarkRead(int id) => Task.FromResult(new BaseResponse());
    public Task<BaseResponse> MarkAllRead() => Task.FromResult(new BaseResponse());
    public Task<BaseResponse> RegisterDevice(RegisterDeviceInput input) => Task.FromResult(new BaseResponse());
    public Task<BaseResponse> ClearDevice() => Task.FromResult(new BaseResponse());
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
