using Wanes.Areas.Services.Bookings.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Bookings;

[TransientInjectable]
public interface IBookingService
{
    /// <summary>
    /// Takes a seat on a trip. Confirmed straight away unless the trip is still
    /// short of the seats its driver asked for, in which case the seat is held
    /// until the threshold resolves.
    /// </summary>
    Task<BaseResponse<BookingOutput>> Create(CreateBookingInput input);

    /// <summary>
    /// Gives the seat back. The rider's one escape hatch, and the same one
    /// whatever put them on the trip — a seat they booked, or a seat a driver
    /// created by claiming the trip they posted at a price they would rather
    /// not pay.
    /// </summary>
    Task<BaseResponse> Cancel(int id);

    Task<BaseResponse<List<BookingOutput>>> GetUserBookings();
}
