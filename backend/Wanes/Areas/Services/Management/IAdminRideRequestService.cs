using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminRideRequestService
{
    Task<BaseResponse<PageOutput<RideRequestRow>>> List(PageInput page, RideRequestStatus? status, int? riderId);
    Task<BaseResponse<RideRequestRow>> Get(int id);
    Task<BaseResponse<RideRequestRow>> Create(RideRequestInput input);
    Task<BaseResponse<RideRequestRow>> Update(int id, RideRequestInput input);
    Task<BaseResponse> Delete(int id);
}
