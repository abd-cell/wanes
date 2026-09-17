using Wanes.Areas.Services.Marketplace.Models;
using Wanes.Areas.Services.Trips.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Trips;

[TransientInjectable]
public interface ITripService
{
    Task<BaseResponse<TripOutput>> Create(CreateTripInput input);
    Task<BaseResponse<TripOutput>> Update(int id, UpdateTripInput input);
    Task<BaseResponse<TripOutput>> Get(int id);
    Task<BaseResponse<List<TripOutput>>> GetUserTrips();
    /// <summary>
    /// Cancels the trip. Once riders depend on it the driver must give a
    /// reason; the cancellation goes on their reliability record, and riders
    /// from a matched request are put back on the market.
    /// </summary>
    Task<BaseResponse> Cancel(int id, CancelTripInput? input = null);

    /// <summary>What cancelling the caller's trip now would cost them.</summary>
    Task<BaseResponse<CancelPreviewOutput>> CancelPreview(int id);

    /// <summary>
    /// The driver has set off for the first pickup. Takes the trip out of search
    /// and off the hail board; no rider's seat moves, because none of them has
    /// been reached yet.
    /// </summary>
    Task<BaseResponse<TripOutput>> Depart(int id);

    Task<BaseResponse<TripOutput>> Start(int id);
    Task<BaseResponse<TripOutput>> Arrive(int id);
    Task<BaseResponse<TripOutput>> Complete(int id);

    /// <summary>The riders holding seats on the caller's own trip.</summary>
    Task<BaseResponse<List<TripBookingRow>>> GetTripBookings(int tripId);

    /// <summary>
    /// Tracks one rider's seat: reached them, picked them up, dropped them off,
    /// or they never showed. The trip's own status follows from these — a trip
    /// with a rider aboard is Active, and the last drop-off completes it.
    /// </summary>
    /// <remarks>
    /// Boarding a rider (<see cref="BookingStatus.InProgress"/>) needs the code
    /// they read out, while the boarding-code setting is on.
    /// </remarks>
    Task<BaseResponse<TripBookingRow>> SetBookingStatus(int tripId, int bookingId, BookingStatus status,
        string? boardingCode = null);

    /// <summary>Where the driver last reported, for a rider tracking the trip.</summary>
    Task<BaseResponse<DriverLocationOutput>> GetDriverLocation(int tripId);
}
