using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/bookings")]
public class AdminBookingsController : BaseApiController
{
    private readonly IAdminBookingService service;

    public AdminBookingsController(IAdminBookingService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<PageOutput<BookingRow>>> List(
        [FromQuery] PageInput page,
        [FromQuery] BookingStatus? status,
        [FromQuery] int? tripId,
        [FromQuery] int? riderId)
        => await service.List(page, status, tripId, riderId);

    [HttpGet("{id:int}")]
    public async Task<BaseResponse<BookingRow>> Get(int id) => await service.Get(id);

    [HttpPost]
    public async Task<BaseResponse<BookingRow>> Create([FromBody] BookingInput input) => await service.Create(input);

    [HttpPut("{id:int}")]
    public async Task<BaseResponse<BookingRow>> Update(int id, [FromBody] BookingInput input)
        => await service.Update(id, input);

    [HttpDelete("{id:int}")]
    public async Task<BaseResponse> Delete(int id) => await service.Delete(id);
}
