using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminTripService
{
    Task<BaseResponse<PageOutput<TripRow>>> List(PageInput page, TripStatus? status, int? driverId);
    Task<BaseResponse<TripRow>> Get(int id);
    Task<BaseResponse<TripRow>> Create(TripInput input);
    Task<BaseResponse<TripRow>> Update(int id, TripInput input);
    Task<BaseResponse> Delete(int id);
}
