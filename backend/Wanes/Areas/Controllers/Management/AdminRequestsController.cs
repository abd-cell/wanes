using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/requests")]
public class AdminRequestsController : BaseApiController
{
    private readonly IAdminRideRequestService service;

    public AdminRequestsController(IAdminRideRequestService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<RideRequestRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] RideRequestStatus? status,
        [FromQuery] int? riderId)
        => await service.List(page, status, riderId);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<RideRequestRow>> Get(int id) => await service.Get(id);

    [HttpPost]
    public async Task<BaseResponse<RideRequestRow>> Create([FromBody] RideRequestInput input) => await service.Create(input);

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<RideRequestRow>> Update(int id, [FromBody] RideRequestInput input)
        => await service.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);
}
