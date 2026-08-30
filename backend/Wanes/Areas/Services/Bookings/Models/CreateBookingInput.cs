namespace Wanes.Areas.Services.Bookings.Models;

public class CreateBookingInput
{
    public int TripId { get; set; }
    public int Seats { get; set; } = 1;
}
