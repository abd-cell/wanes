using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/notifications")]
public class AdminNotificationsController : BaseApiController
{
    private readonly IAdminNotificationService service;

    public AdminNotificationsController(IAdminNotificationService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<NotificationRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] NotificationType? type,
        [FromQuery] int? userId,
        [FromQuery] bool? isRead)
        => await service.List(page, type, userId, isRead);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<NotificationRow>> Get(int id) => await service.Get(id);

    [HttpPost]
    public async Task<BaseResponse<NotificationRow>> Create([FromBody] NotificationInput input) => await service.Create(input);

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<NotificationRow>> Update(int id, [FromBody] NotificationInput input)
        => await service.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);
}
