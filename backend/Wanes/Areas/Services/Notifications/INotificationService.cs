using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Services.Notifications.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Notifications;

[TransientInjectable]
public interface INotificationService
{
    Task Notify(int userId, NotificationType type, string title, string body, object? data = null);

    /// <summary>Notifies verified, online drivers near the request origin. Returns the count reached.</summary>
    Task<int> NotifyNearbyDrivers(RideRequest request);

    Task<BaseResponse<List<NotificationRow>>> GetUserNotifications();
    Task<BaseResponse> MarkRead(int id);
}
