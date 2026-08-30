using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Logging;
using Wanes.Areas.Services.Logging.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Logging;

/// <summary>
/// Admin "checker" over the API request log: browse recent calls and read an
/// aggregate health snapshot. Read-only — rows are written by the middleware.
/// </summary>
[AppAuthorize(Roles.Admin)]
[Route("api/v1/api-logs")]
public class ApiLogsController : BaseApiController
{
    private readonly IApiLogService apiLogService;

    public ApiLogsController(IApiLogService apiLogService) => this.apiLogService = apiLogService;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<ApiLogRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] string? method,
        [FromQuery] string? path,
        [FromQuery] int? statusCode,
        [FromQuery] int? actorUserId)
        => await apiLogService.GetLogs(page, method, path, statusCode, actorUserId);

    [HttpGet("summary")]
    public async Task<BaseResponse<ApiLogSummary>> Summary([FromQuery] int sampleSize = 500)
        => await apiLogService.GetSummary(sampleSize);
}
