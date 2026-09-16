using Wanes.Areas.Services.Search.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Search;

/// <summary>
/// A driver searching for riders: trips still waiting for a driver that lie
/// along the journey this driver is about to make.
///
/// Separate from <see cref="ISearchService"/> on purpose. They look symmetric —
/// two points, a time, a corridor — but they answer to different rules: what a
/// driver may take turns on their car, their verification and their diary, none
/// of which a rider's search knows anything about. Folding both into one service
/// would mean one class holding two sets of eligibility rules and a flag saying
/// which half to run.
/// </summary>
[TransientInjectable]
public interface IDemandSearchService
{
    /// <summary>
    /// Trips wanting the journey this driver is about to drive, best first.
    ///
    /// Everything it returns, taking must be able to honour — the same promise
    /// the board makes (<c>RiderTripService.GetNearby</c>), because a list that
    /// offers what the API then refuses is worse than a short one.
    /// </summary>
    Task<BaseResponse<DemandSearchResult>> Search(DemandSearchInput input);
}
