using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Areas.Services.Users.Driver.Models;
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
    public async Task<BaseResponse<PageOutput<DriverRow>>> PendingDrivers(
        [FromQuery] PageInput page, [FromQuery] DriverStatus? status)
        => await adminService.GetPendingDrivers(page, status);

    /// <summary>The documents one driver uploaded, so a reviewer can look before deciding.</summary>
    [HttpGet("drivers/{userId:int}/documents")]
    public async Task<BaseResponse<List<DriverDocumentOutput>>> DriverDocuments(int userId)
        => await adminService.GetDriverDocuments(userId);

    /// <summary>
    /// Streams one driver document to the reviewer.
    ///
    /// Outside the <see cref="BaseResponse"/> envelope for the same reason as the
    /// driver's own copy of this endpoint: the body is a JPEG or a PDF. Failures
    /// still answer with the envelope. The bytes are never public — this is the
    /// only way to them, and it is admin-only and audited.
    /// </summary>
    [HttpGet("drivers/documents/{documentId:int}/content")]
    public async Task<IActionResult> DriverDocumentContent(int documentId)
    {
        var response = await adminService.OpenDriverDocument(documentId);
        if (!response.Success || response.Data == null) return Ok(response);
        return File(response.Data.Content, response.Data.ContentType);
    }

    [HttpPost("drivers/{userId:int}/verify")]
    public async Task<BaseResponse> VerifyDriver(int userId, [FromBody] VerifyInput input)
        => await adminService.VerifyDriver(userId, input);

    [HttpGet("audit")]
    public async Task<BaseResponse<PageOutput<AuditRow>>> Audit(
        [FromQuery] PageInput page, [FromQuery] int? actorUserId, [FromQuery] string? action)
        => await adminService.GetAuditLog(page, actorUserId, action);
}
