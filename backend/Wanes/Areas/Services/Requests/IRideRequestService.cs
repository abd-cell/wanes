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
}
