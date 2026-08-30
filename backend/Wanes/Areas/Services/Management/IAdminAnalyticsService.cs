using Wanes.Areas.Services.Management.Models.Analytics;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

/// <summary>
/// Read-only aggregations behind the CMS dashboard. Every method is scoped to a
/// rolling window of <c>days</c> (inclusive of today); lifetime totals are also
/// returned by <see cref="GetOverview"/> so the dashboard can show both.
/// </summary>
[TransientInjectable]
public interface IAdminAnalyticsService
{
    Task<BaseResponse<OverviewOutput>> GetOverview(int days);
    Task<BaseResponse<TimeSeriesOutput>> GetTimeSeries(int days);
    Task<BaseResponse<BreakdownsOutput>> GetBreakdowns(int days);
    Task<BaseResponse<OperationsOutput>> GetOperations(int days);
    Task<BaseResponse<LeaderboardsOutput>> GetLeaderboards(int days);
}
