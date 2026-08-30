using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminVehicleService
{
    Task<BaseResponse<PageOutput<VehicleRow>>> List(PageInput page, int? userId);
    Task<BaseResponse<VehicleRow>> Get(int id);
    Task<BaseResponse<VehicleRow>> Create(VehicleInput input);
    Task<BaseResponse<VehicleRow>> Update(int id, VehicleInput input);
    Task<BaseResponse> Delete(int id);
}
