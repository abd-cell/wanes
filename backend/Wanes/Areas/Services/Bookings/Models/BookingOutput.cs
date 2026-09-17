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
    /// Only filled on a seat that is both live and committed: a cancelled or
    /// completed seat is no longer a reason to hold someone's phone number, and
    /// a pending one is not yet a reason — nobody has agreed to anything.
    /// Deliberately absent from <see cref="Trips.Models.TripOutput"/>, whose GET
    /// is anonymous.
    /// </summary>
    public string? DriverPhone { get; set; }

    /// <summary>What the seat costs. Display-only — there are no payments.</summary>
    public decimal? PricePerSeat { get; set; }

    /// <summary>
    /// Seats the trip needs before it is on, and how many it has. Zero
    /// threshold means there is no condition; the rider's card only shows
    /// "confirms at 3 of 4" when there is something to confirm.
    /// </summary>
    public int MinSeatsToConfirm { get; set; }

    public int SeatsHeld { get; set; }

    /// <summary>
    /// The code the rider reads to the driver at pickup. Only on a seat that
    /// is committed and not yet boarded — it is useless before, and must not
    /// linger after.
    /// </summary>
    public string? BoardingCode { get; set; }

    /// <summary>The rider has a live "follow my trip" link.</summary>
    public bool HasShareLink { get; set; }

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
        PricePerSeat = trip?.PricePerSeat;
        MinSeatsToConfirm = trip?.MinSeatsToConfirm ?? 0;

        // Held seats read off the trip's own arithmetic rather than a second
        // query: every held seat, pending or confirmed, is already off SeatsLeft.
        SeatsHeld = trip == null ? 0 : trip.SeatsTotal - trip.SeatsLeft;

        // A seat still waiting on the trip's threshold is not yet a reason to
        // hold somebody's number: nobody has committed to anything.
        var committed = BookingStatusRules.IsLive(booking.Status)
                        && !BookingStatusRules.IsPending(booking.Status);
        if (committed) DriverPhone = trip?.Driver?.Phone;
        if (booking.Status is BookingStatus.Confirmed or BookingStatus.Arrived)
            BoardingCode = booking.BoardingCode;
        HasShareLink = !string.IsNullOrEmpty(booking.ShareToken);
    }
}
