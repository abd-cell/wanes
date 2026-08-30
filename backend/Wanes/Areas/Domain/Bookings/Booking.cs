using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Bookings;

/// <summary>Links a rider to a trip, reserving one or more seats.</summary>
public class Booking : AuditableEntity
{
    public int TripId { get; set; }
    public Trip? Trip { get; set; }

    public int RiderId { get; set; }
    public User? Rider { get; set; }

    public int Seats { get; set; } = 1;
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    /// <summary>Set when this booking was created by accepting a ride request.</summary>
    public int? RideRequestId { get; set; }
}
