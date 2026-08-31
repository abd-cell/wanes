using Wanes.Areas.Domain.Bookings;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Trips.Models;

/// <summary>
/// One rider's seat on a trip, as the driver sees it.
///
/// Returned only to the trip's own driver (<c>GET Trips/{id}/bookings</c>) —
/// this is the mirror of <see cref="Bookings.Models.BookingOutput.DriverPhone"/>:
/// the two parties to a live booking can reach each other, and nobody else can.
/// </summary>
public class TripBookingRow
{
    public int Id { get; set; }
    public int RiderId { get; set; }
    public string RiderName { get; set; } = string.Empty;
    public double RiderRating { get; set; }

    /// <summary>Null once the seat is cancelled or completed — see the class note.</summary>
    public string? RiderPhone { get; set; }

    public int Seats { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime BookedAt { get; set; }

    public TripBookingRow() { }

    public TripBookingRow(Booking booking)
    {
        Id = booking.Id;
        RiderId = booking.RiderId;
        RiderName = booking.Rider?.DisplayName
            ?? $"{booking.Rider?.FirstName} {booking.Rider?.LastName}".Trim();
        RiderRating = booking.Rider?.RatingAvg ?? 0;
        Seats = booking.Seats;
        Status = booking.Status;
        BookedAt = booking.CreationDate;

        if (BookingStatusRules.IsLive(booking.Status)) RiderPhone = booking.Rider?.Phone;
    }
}
