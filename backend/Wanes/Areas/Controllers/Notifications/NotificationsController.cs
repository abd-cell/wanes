using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Notifications.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Notifications;

[AppAuthorize]
public class NotificationsController : BaseApiController
{
    private readonly INotificationService notificationService;

    public NotificationsController(INotificationService notificationService)
        => this.notificationService = notificationService;

    [HttpGet("mine")]
    public async Task<BaseResponse<NotificationFeed>> Mine()
        => await notificationService.GetUserNotifications();

    [HttpPost("{id:int}/read")]
    public async Task<BaseResponse> MarkRead(int id)
        => await notificationService.MarkRead(id);

    [HttpPost("read-all")]
    public async Task<BaseResponse> MarkAllRead()
        => await notificationService.MarkAllRead();

    /// <summary>
    /// Registers the caller's FCM token. The app calls this on every launch and
    /// again whenever Firebase rotates the token, so it is an upsert.
    /// </summary>
    [HttpPost("device-token")]
    public async Task<BaseResponse> RegisterDevice(RegisterDeviceInput input)
        => await notificationService.RegisterDevice(input);

    /// <summary>Stops push for this device without ending the session.</summary>
    [HttpDelete("device-token")]
    public async Task<BaseResponse> ClearDevice()
        => await notificationService.ClearDevice();
}
