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
    public async Task<BaseResponse<List<NotificationRow>>> Mine()
        => await notificationService.GetUserNotifications();

    [HttpPost("{id:int}/read")]
    public async Task<BaseResponse> MarkRead(int id)
        => await notificationService.MarkRead(id);
}
