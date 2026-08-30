using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Vehicles;
using Wanes.Areas.Services.Vehicles.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Vehicles;

[AppAuthorize]
[Route("api/v1/me/vehicles")]
public class VehiclesController : BaseApiController
{
    private readonly IVehicleService vehicleService;

    public VehiclesController(IVehicleService vehicleService) => this.vehicleService = vehicleService;

    [HttpGet]
    public async Task<BaseResponse<List<VehicleOutput>>> List() => await vehicleService.GetUserVehicles();

    [HttpPost]
    public async Task<BaseResponse<VehicleOutput>> Add([FromBody] VehicleInput input)
        => await vehicleService.Create(input);

    [HttpPatch("{id:int}")]
    public async Task<BaseResponse<VehicleOutput>> Update(int id, [FromBody] VehicleInput input)
        => await vehicleService.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await vehicleService.Delete(id);
}
