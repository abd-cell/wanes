using Wanes.Areas.Services.Requests.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Requests;

[TransientInjectable]
public interface IRideRequestService
{
    Task<BaseResponse<List<RideRequestRow>>> GetUserRequests();
    Task<BaseResponse> Cancel(int id);
    Task<BaseResponse<List<RideRequestRow>>> GetNearby(double lat, double lng, int radiusMeters);
    Task<BaseResponse<RideRequestRow>> Accept(int id);

    /// <summary>
    /// Moves every hail past its expiry from Open to Expired and closes it on the
    /// drivers' screens. Returns how many were swept.
    ///
    /// Called on a timer by <see cref="RideRequestExpiryWorker"/> rather than
    /// lazily at read time: an expired request has to stop being offered even to
    /// a driver whose app never asks again, and the rider's screen learns the
    /// hail is over from the same broadcast instead of from its own guesswork.
    /// </summary>
    Task<int> ExpireDue();
}
