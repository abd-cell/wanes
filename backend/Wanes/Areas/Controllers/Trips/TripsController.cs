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
    [HttpGet("{id:int}/bookings")]
    public async Task<BaseResponse<List<TripBookingRow>>> Bookings(int id)
        => await tripService.GetTripBookings(id);

    /// <summary>Driver moves one rider's seat along (picked up, dropped off, no-show).</summary>
    [AppAuthorize]
    [HttpPut("{id:int}/bookings/{bookingId:int}/status")]
    public async Task<BaseResponse<TripBookingRow>> SetBookingStatus(
        int id, int bookingId, [FromBody] SetBookingStatusInput input)
        => await tripService.SetBookingStatus(id, bookingId, input.Status);

    [AppAuthorize]
    [HttpGet("{id:int}/driver-location")]
    public async Task<BaseResponse<DriverLocationOutput>> DriverLocation(int id)
        => await tripService.GetDriverLocation(id);

    [AppAuthorize]
    [HttpPost("{id:int}/cancel")]
    public async Task<BaseResponse> Cancel(int id) => await tripService.Cancel(id);

    [AppAuthorize]
    /// <summary>The driver has set off for the first pickup.</summary>
    [HttpPost("{id:int}/depart")]
    public async Task<BaseResponse<TripOutput>> Depart(int id) => await tripService.Depart(id);

    [HttpPost("{id:int}/start")]
    public async Task<BaseResponse<TripOutput>> Start(int id) => await tripService.Start(id);

    [AppAuthorize]
    [HttpPost("{id:int}/arrive")]
    public async Task<BaseResponse<TripOutput>> Arrive(int id) => await tripService.Arrive(id);

    [AppAuthorize]
    [HttpPost("{id:int}/complete")]
    public async Task<BaseResponse<TripOutput>> Complete(int id) => await tripService.Complete(id);
}
