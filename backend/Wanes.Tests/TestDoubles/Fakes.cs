using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Notifications.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
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
    public List<string> Sent { get; } = [];
    public int NearbyDriverCount { get; set; } = 2;

    public Task Notify(int userId, NotificationType type, string title, string body, object? data = null)
    {
        Sent.Add($"{userId}:{type}");
        return Task.CompletedTask;
    }

    public Task<int> NotifyNearbyDrivers(RideRequest request) =>
        Task.FromResult(NearbyDriverCount);

    public Task<BaseResponse<List<NotificationRow>>> GetUserNotifications() =>
        Task.FromResult(new BaseResponse<List<NotificationRow>>(new List<NotificationRow>()));

    public Task<BaseResponse> MarkRead(int id) => Task.FromResult(new BaseResponse());
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
    public string Generate(int userId, IEnumerable<Roles> roles, string sessionKey) =>
        $"token-{userId}-{sessionKey}";
}
