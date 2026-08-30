using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Trips;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Bookings.Models;

public class BookingOutput
{
    public int Id { get; set; }
    public int TripId { get; set; }
    public int RiderId { get; set; }
    public int Seats { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime DepartAt { get; set; }
    public string OriginAddress { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;

    public BookingOutput() { }

    public BookingOutput(Booking booking, Trip? trip)
    {
        if (booking == null) return;

        Id = booking.Id;
        TripId = booking.TripId;
        RiderId = booking.RiderId;
        Seats = booking.Seats;
        Status = booking.Status;
        DepartAt = trip?.DepartAt ?? default;
        OriginAddress = trip?.OriginAddress ?? string.Empty;
        DestinationAddress = trip?.DestinationAddress ?? string.Empty;
    }
}
