using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.RideRequests;
using Wanes.Areas.Services.RideRequests.Models;
using Wanes.Areas.Services.Search;
using Wanes.Areas.Services.Search.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.RideRequests;

/// <summary>
/// The demand marketplace. Riders create and join requests here; drivers
/// discover them and offer to serve them.
///
/// One controller for both sides because it is one resource. Splitting it by
/// audience would put <c>nearby</c> and <c>interest</c> somewhere that does not
/// own the row they act on.
/// </summary>
[AppAuthorize]
[Route("api/v1/ride-requests")]
public class RideRequestsController : BaseApiController
{
    private readonly IRideRequestService rideRequestService;
    private readonly IDriverInterestService driverInterestService;
    private readonly IDemandSearchService demandSearchService;

    public RideRequestsController(
        IRideRequestService rideRequestService,
        IDriverInterestService driverInterestService,
        IDemandSearchService demandSearchService)
    {
        this.rideRequestService = rideRequestService;
        this.driverInterestService = driverInterestService;
        this.demandSearchService = demandSearchService;
    }

    /// <summary>Creates demand. The caller is its first participant.</summary>
    [HttpPost]
    public async Task<BaseResponse<RideRequestRow>> Create([FromBody] CreateRideRequestInput input)
        => await rideRequestService.Create(input);

    /// <summary>Every request the caller is on.</summary>
    [HttpGet("mine")]
    public async Task<BaseResponse<List<RideRequestRow>>> Mine()
        => await rideRequestService.GetMine();

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<RideRequestRow>> Get(int id)
        => await rideRequestService.Get(id);

    /// <summary>Joins somebody else's request rather than creating a duplicate.</summary>
    [HttpPost("{id:int}/join")]
    public async Task<BaseResponse<RideRequestRow>> Join(int id, [FromBody] JoinRideRequestInput input)
        => await rideRequestService.Join(id, input);

    /// <summary>Gives the caller's seats back; the request closes with the last of them.</summary>
    [HttpPost("{id:int}/leave")]
    public async Task<BaseResponse> Leave(int id)
        => await rideRequestService.Leave(id);

    /// <summary>
    /// The driver's search: requests wanting the journey they are about to
    /// drive.
    ///
    /// A different question from <c>nearby</c> below — that one answers "who
    /// needs a lift around me, now", off the driver's live position; this one
    /// answers "I am driving Amman → Irbid at six, who is going my way".
    /// </summary>
    [HttpPost("search")]
    public async Task<BaseResponse<DemandSearchResult>> Search([FromBody] DemandSearchInput input)
        => await demandSearchService.Search(input);

    /// <summary>The driver's board: requests near them they could actually serve.</summary>
    [HttpGet("nearby")]
    public async Task<BaseResponse<List<RideRequestRow>>> Nearby(
        [FromQuery] double lat, [FromQuery] double lng, [FromQuery] int radiusMeters = 5000)
        => await rideRequestService.GetNearby(lat, lng, radiusMeters);

    /// <summary>
    /// Offers to serve the request, in this car at this price.
    ///
    /// Where the marketplace selects immediately — the shipped setting — this
    /// also selects the driver and forms the trip, and the response carries the
    /// trip's id in <c>matchedTripId</c>. Where offers accumulate, it records
    /// the offer and the decision comes later.
    /// </summary>
    [HttpPost("{id:int}/interest")]
    public async Task<BaseResponse<RideRequestRow>> ExpressInterest(int id,
        [FromBody] ExpressInterestInput? input = null)
        => await driverInterestService.ExpressInterest(id, input);

    /// <summary>Takes the offer back, while nobody has been selected.</summary>
    [HttpDelete("{id:int}/interest")]
    public async Task<BaseResponse> WithdrawInterest(int id)
        => await driverInterestService.WithdrawInterest(id);
}
