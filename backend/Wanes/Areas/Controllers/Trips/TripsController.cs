using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Trips;
using Wanes.Areas.Services.Trips.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Trips;

public class TripsController : BaseApiController
{
    private readonly ITripService tripService;
    private readonly ITripConfirmationService tripConfirmationService;

    public TripsController(ITripService tripService, ITripConfirmationService tripConfirmationService)
    {
        this.tripService = tripService;
        this.tripConfirmationService = tripConfirmationService;
    }

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

    /// <summary>
    /// The driver running a trip that never reached the seats they asked for.
    /// Every held seat is committed and the condition is dropped.
    /// </summary>
    [AppAuthorize]
    [HttpPost("{id:int}/confirm")]
    public async Task<BaseResponse<TripOutput>> Confirm(int id)
        => await tripConfirmationService.ConfirmNow(id);

    /// <summary>
    /// The driver calling a trip off for want of riders. Distinct from an
    /// ordinary cancel: the riders are told why, in the words of the empty
    /// seats rather than of a driver who changed their mind about them.
    /// </summary>
    [AppAuthorize]
    [HttpPost("{id:int}/cancel-low-seats")]
    public async Task<BaseResponse<TripOutput>> CancelForLowSeats(int id)
        => await tripConfirmationService.CancelForLowSeats(id);

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
