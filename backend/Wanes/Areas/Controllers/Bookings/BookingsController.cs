using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Bookings;
using Wanes.Areas.Services.Bookings.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Bookings;

[AppAuthorize]
public class BookingsController : BaseApiController
{
    private readonly IBookingService bookingService;

    public BookingsController(IBookingService bookingService) => this.bookingService = bookingService;

    [HttpPost]
    public async Task<BaseResponse<BookingOutput>> Create([FromBody] CreateBookingInput input)
        => await bookingService.Create(input);

    [HttpPost("{id:int}/cancel")]
    public async Task<BaseResponse> Cancel(int id)
        => await bookingService.Cancel(id);


    [HttpGet("mine")]
    public async Task<BaseResponse<List<BookingOutput>>> Mine()
        => await bookingService.GetUserBookings();
}
