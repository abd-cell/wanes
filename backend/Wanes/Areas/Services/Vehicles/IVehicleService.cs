using Wanes.Areas.Services.Vehicles.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Vehicles;

[TransientInjectable]
public interface IVehicleService
{
    Task<BaseResponse<List<VehicleOutput>>> GetUserVehicles();
    Task<BaseResponse<VehicleOutput>> Create(VehicleInput input);
    Task<BaseResponse<VehicleOutput>> Update(int id, VehicleInput input);
    Task<BaseResponse> Delete(int id);
}
