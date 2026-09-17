using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Bookings;
using Wanes.Areas.Services.Bookings.Models;
using Wanes.Areas.Services.Series;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Bookings;

[AppAuthorize]
public class BookingsController : BaseApiController
{
    private readonly IBookingService bookingService;
    private readonly ISeriesInfoService seriesInfo;

    public BookingsController(IBookingService bookingService, ISeriesInfoService seriesInfo)
    {
        this.bookingService = bookingService;
        this.seriesInfo = seriesInfo;
    }

    [HttpPost]
    public async Task<BaseResponse<BookingOutput>> Create([FromBody] CreateBookingInput input)
        => await bookingService.Create(input);

    [HttpPost("{id:int}/cancel")]
    public async Task<BaseResponse> Cancel(int id)
        => await bookingService.Cancel(id);


    [HttpGet("mine")]
    public async Task<BaseResponse<List<BookingOutput>>> Mine()
        => await seriesInfo.With(await bookingService.GetUserBookings());
}
