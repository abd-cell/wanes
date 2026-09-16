using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Domain.Bookings;

/// <summary>
/// When a rider may take another seat. The mirror of
/// <see cref="Wanes.Areas.Domain.Trips.DriverAvailabilityRules"/>, and for the
/// same physical reason: a rider sits in one car at a time, so the two ways
/// they are unavailable are that they are riding right now, or they are already
/// holding a seat on something leaving at about the same moment.
///
/// A rider holds their time in two shapes, and both count here. A
/// <see cref="Booking"/> is a seat on a real trip. A hold on an *open*
/// rider-posted trip is not a seat yet, but it is the rider saying "I am
/// leaving then" and waiting for a driver to say yes — a rider who also books
/// a seat at that hour has promised two drivers the same trip. Claimed
/// rider-posted trips are deliberately not counted twice: claiming turns every
/// hold into a booking, so the booking side already has them.
/// </summary>
public static class RiderAvailabilityRules
{
    /// <summary>
    /// The rider is in the car, or the driver is at their kerb waiting for them
    /// to get in. Either way they cannot be somewhere else.
    /// </summary>
    public static bool IsEngaged(BookingStatus status) =>
        status is BookingStatus.InProgress or BookingStatus.Arrived;

    /// <summary>
    /// The seat still occupies the rider's day — anything not travelled, not
    /// called off and not missed.
    /// </summary>
    public static bool HoldsSchedule(BookingStatus status) => HoldingStatuses.Contains(status);

    /// <summary>
    /// <see cref="HoldsSchedule"/> as data, because a query cannot call it.
    /// Completed, Cancelled and NoShow are all finished with: the seat is over,
    /// however it ended.
    /// </summary>
    public static readonly BookingStatus[] HoldingStatuses =
    [
        BookingStatus.Pending, BookingStatus.Confirmed,
        BookingStatus.Arrived, BookingStatus.InProgress,
    ];

    /// <summary><see cref="IsEngaged(BookingStatus)"/> as data, for the same reason.</summary>
    public static readonly BookingStatus[] EngagedStatuses =
    [
        BookingStatus.Arrived, BookingStatus.InProgress,
    ];

    /// <summary>
    /// How close two of one rider's departures may be. The same number the
    /// driver clash uses, taken from the same place: a rider searching at T is
    /// offered trips leaving within <see cref="MatchRules.TimeWindow"/> of T, so
    /// two seats that close together are one rider trying to be in two cars.
    /// </summary>
    public static readonly TimeSpan ClashWindow = MatchRules.TimeWindow;

    /// <summary>Whether two departures are too close together for one rider.</summary>
    public static bool Clashes(DateTime a, DateTime b) => (a - b).Duration() < ClashWindow;
}
