using Wanes.Areas.Domain.Bookings;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>Admin view of a booking (list + detail share one shape).</summary>
public class BookingRow
{
    public int Id { get; set; }
    public int TripId { get; set; }
    public string? TripSummary { get; set; }
    public int RiderId { get; set; }
    public string? RiderName { get; set; }
    public int Seats { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreationDate { get; set; }

    public BookingRow() { }

    public BookingRow(Booking b)
    {
        Id = b.Id;
        TripId = b.TripId;
        RiderId = b.RiderId;
        Seats = b.Seats;
        Status = b.Status;
        CreationDate = b.CreationDate;
    }
}

/// <summary>Create/update payload for a booking.</summary>
public class BookingInput
{
    public int TripId { get; set; }
    public int RiderId { get; set; }
    public int Seats { get; set; } = 1;
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
}
