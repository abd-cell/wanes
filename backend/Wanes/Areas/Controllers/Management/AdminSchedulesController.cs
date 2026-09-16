using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/schedules")]
public class AdminSchedulesController : BaseApiController
{
    private readonly IAdminScheduleService service;

    public AdminSchedulesController(IAdminScheduleService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<ScheduleRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] ActiveRole? ownerRole,
        [FromQuery] int? ownerId)
        => await service.List(page, ownerRole, ownerId);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<ScheduleRow>> Get(int id) => await service.Get(id);

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<ScheduleRow>> Update(int id, [FromBody] ScheduleInput input)
        => await service.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);
}
