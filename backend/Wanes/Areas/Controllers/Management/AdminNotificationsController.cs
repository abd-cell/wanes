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
        [FromQuery] bool? isRead,
        [FromQuery] bool? isDeleted)
        => await service.List(page, type, userId, isRead, isDeleted);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<NotificationRow>> Get(int id) => await service.Get(id);

    [HttpPost]
    public async Task<BaseResponse<NotificationRow>> Create([FromBody] NotificationInput input) => await service.Create(input);

    /// <summary>Fans one notification out to an audience — "notify every driver", etc.</summary>
    [HttpPost("broadcast")]
    public async Task<BaseResponse<BroadcastResult>> Broadcast([FromBody] BroadcastInput input)
        => await service.Broadcast(input);

    /// <summary>Fans one notification out to a hand-picked set of users.</summary>
    [HttpPost("send")]
    public async Task<BaseResponse<TargetedSendResult>> Send([FromBody] TargetedSendInput input)
        => await service.SendTargeted(input);

    /// <summary>Marks read/unread or deletes every row the admin ticked.</summary>
    [HttpPost("bulk")]
    public async Task<BaseResponse<BulkNotificationResult>> Bulk([FromBody] BulkNotificationInput input)
        => await service.Bulk(input);

    /// <summary>Headline counts for the manager, describing the whole table.</summary>
    [HttpGet("stats")]
    public async Task<BaseResponse<NotificationStats>> Stats() => await service.Stats();

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<NotificationRow>> Update(int id, [FromBody] NotificationInput input)
        => await service.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);
}
