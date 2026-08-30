using Wanes.Areas.Services.Trips.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Trips;

[TransientInjectable]
public interface ITripService
{
    Task<BaseResponse<TripOutput>> Create(CreateTripInput input);
    Task<BaseResponse<TripOutput>> Update(int id, UpdateTripInput input);
    Task<BaseResponse<TripOutput>> Get(int id);
    Task<BaseResponse<List<TripOutput>>> GetUserTrips();
    Task<BaseResponse> Cancel(int id);
    Task<BaseResponse<TripOutput>> Start(int id);
    Task<BaseResponse<TripOutput>> Complete(int id);
}
