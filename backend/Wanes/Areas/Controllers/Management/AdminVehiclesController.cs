using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/vehicles")]
public class AdminVehiclesController : BaseApiController
{
    private readonly IAdminVehicleService service;

    public AdminVehiclesController(IAdminVehicleService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<VehicleRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] int? userId)
        => await service.List(page, userId);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<VehicleRow>> Get(int id) => await service.Get(id);

    [HttpPost]
    public async Task<BaseResponse<VehicleRow>> Create([FromBody] VehicleInput input) => await service.Create(input);

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<VehicleRow>> Update(int id, [FromBody] VehicleInput input)
        => await service.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);
}
