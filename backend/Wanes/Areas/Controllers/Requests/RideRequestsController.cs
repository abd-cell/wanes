using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Requests;
using Wanes.Areas.Services.Requests.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Requests;

[AppAuthorize]
[Route("api/v1/requests")]
public class RideRequestsController : BaseApiController
{
    private readonly IRideRequestService rideRequestService;

    public RideRequestsController(IRideRequestService rideRequestService)
        => this.rideRequestService = rideRequestService;

    [HttpGet("mine")]
    public async Task<BaseResponse<List<RideRequestRow>>> Mine()
        => await rideRequestService.GetUserRequests();

    [HttpPost("{id:int}/cancel")]
    public async Task<BaseResponse> Cancel(int id)
        => await rideRequestService.Cancel(id);

    [HttpGet("nearby")]
    public async Task<BaseResponse<List<RideRequestRow>>> Nearby(
        [FromQuery] double lat, [FromQuery] double lng, [FromQuery] int radiusMeters = 5000)
        => await rideRequestService.GetNearby(lat, lng, radiusMeters);

    [HttpPost("{id:int}/accept")]
    public async Task<BaseResponse<RideRequestRow>> Accept(int id)
        => await rideRequestService.Accept(id);
}
