using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/trips")]
public class AdminTripsController : BaseApiController
{
    private readonly IAdminTripService service;

    public AdminTripsController(IAdminTripService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<TripRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] TripStatus? status,
        [FromQuery] int? driverId)
        => await service.List(page, status, driverId);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<TripRow>> Get(int id) => await service.Get(id);

    [HttpPost]
    public async Task<BaseResponse<TripRow>> Create([FromBody] TripInput input) => await service.Create(input);

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<TripRow>> Update(int id, [FromBody] TripInput input)
        => await service.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);
}
