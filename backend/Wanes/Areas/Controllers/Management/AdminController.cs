using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin")]
public class AdminController : BaseApiController
{
    private readonly IAdminService adminService;

    public AdminController(IAdminService adminService) => this.adminService = adminService;

    [HttpGet("drivers/pending")]
    public async Task<BaseResponse<PageOutput<DriverRow>>> PendingDrivers([FromQuery] PageInput page)
        => await adminService.GetPendingDrivers(page);

    [HttpPost("drivers/{userId:int}/verify")]
    public async Task<BaseResponse> VerifyDriver(int userId, [FromBody] VerifyInput input)
        => await adminService.VerifyDriver(userId, input);

    [HttpGet("audit")]
    public async Task<BaseResponse<PageOutput<AuditRow>>> Audit(
        [FromQuery] PageInput page, [FromQuery] int? actorUserId, [FromQuery] string? action)
        => await adminService.GetAuditLog(page, actorUserId, action);
}
