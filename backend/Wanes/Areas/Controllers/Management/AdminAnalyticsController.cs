using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models.Analytics;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

/// <summary>
/// Dashboard aggregations. Split into five endpoints so the CMS can load each
/// section independently instead of blocking on one large payload. Every endpoint
/// takes the same <c>days</c> window so the numbers across the page always agree.
/// </summary>
[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/analytics")]
public class AdminAnalyticsController : BaseApiController
{
    private readonly IAdminAnalyticsService analyticsService;

    public AdminAnalyticsController(IAdminAnalyticsService analyticsService)
        => this.analyticsService = analyticsService;

    [HttpGet("overview")]
    public async Task<BaseResponse<OverviewOutput>> Overview([FromQuery] int days = 30)
        => await analyticsService.GetOverview(days);

    [HttpGet("timeseries")]
    public async Task<BaseResponse<TimeSeriesOutput>> TimeSeries([FromQuery] int days = 30)
        => await analyticsService.GetTimeSeries(days);

    [HttpGet("breakdowns")]
    public async Task<BaseResponse<BreakdownsOutput>> Breakdowns([FromQuery] int days = 30)
        => await analyticsService.GetBreakdowns(days);

    [HttpGet("operations")]
    public async Task<BaseResponse<OperationsOutput>> Operations([FromQuery] int days = 30)
        => await analyticsService.GetOperations(days);

    [HttpGet("leaderboards")]
    public async Task<BaseResponse<LeaderboardsOutput>> Leaderboards([FromQuery] int days = 30)
        => await analyticsService.GetLeaderboards(days);
}
