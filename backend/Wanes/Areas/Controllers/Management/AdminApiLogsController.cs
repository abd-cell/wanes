using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/api-logs")]
public class AdminApiLogsController : BaseApiController
{
    private readonly IAdminApiLogService service;

    public AdminApiLogsController(IAdminApiLogService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<ApiLogRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] int? statusCode,
        [FromQuery] string? method)
        => await service.List(page, statusCode, method);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<ApiLogRow>> Get(int id) => await service.Get(id);
}
