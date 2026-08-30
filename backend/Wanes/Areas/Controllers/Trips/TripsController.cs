using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Trips;
using Wanes.Areas.Services.Trips.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Trips;

public class TripsController : BaseApiController
{
    private readonly ITripService tripService;

    public TripsController(ITripService tripService) => this.tripService = tripService;

    [AppAuthorize]
    [HttpPost]
    public async Task<BaseResponse<TripOutput>> Create([FromBody] CreateTripInput input)
        => await tripService.Create(input);

    [AppAuthorize]
    [HttpPut("{id:int}")]
    public async Task<BaseResponse<TripOutput>> Update(int id, [FromBody] UpdateTripInput input)
        => await tripService.Update(id, input);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<TripOutput>> Get(int id) => await tripService.Get(id);

    [AppAuthorize]
    [HttpGet("mine")]
    public async Task<BaseResponse<List<TripOutput>>> Mine() => await tripService.GetUserTrips();

    [AppAuthorize]
    [HttpPost("{id:int}/cancel")]
    public async Task<BaseResponse> Cancel(int id) => await tripService.Cancel(id);

    [AppAuthorize]
    [HttpPost("{id:int}/start")]
    public async Task<BaseResponse<TripOutput>> Start(int id) => await tripService.Start(id);

    [AppAuthorize]
    [HttpPost("{id:int}/complete")]
    public async Task<BaseResponse<TripOutput>> Complete(int id) => await tripService.Complete(id);
}
