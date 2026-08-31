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

    /// <summary>
    /// Where the trip itself has got to, so the rider's tracking rail can be
    /// drawn from the booking row without a second call for the trip.
    /// </summary>
    public TripStatus TripStatus { get; set; }

    /// <summary>
    /// The driver's number, for the call button on the live-trip screen.
    ///
    /// Only filled while the booking is live: a cancelled or completed seat is
    /// no longer a reason to hold someone's phone number. Deliberately absent
    /// from <see cref="Trips.Models.TripOutput"/>, whose GET is anonymous.
    /// </summary>
    public string? DriverPhone { get; set; }

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
        TripStatus = trip?.Status ?? default;

        if (BookingStatusRules.IsLive(booking.Status)) DriverPhone = trip?.Driver?.Phone;
    }
}
