using Wanes.Areas.Services.Bookings.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Bookings;

[TransientInjectable]
public interface IBookingService
{
    Task<BaseResponse<BookingOutput>> Create(CreateBookingInput input);
    Task<BaseResponse> Cancel(int id);
    Task<BaseResponse<List<BookingOutput>>> GetUserBookings();
}
