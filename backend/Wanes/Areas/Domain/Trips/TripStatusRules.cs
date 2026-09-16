using Wanes.Areas.Domain.Bookings;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Domain.Trips;

/// <summary>
/// A trip's status is a summary of its riders' seats, not a field the driver
/// sets by hand: one trip carries several riders, so "under way" means someone
/// is aboard and "finished" means nobody is left to drop off. Every service
/// that touches a booking re-derives the trip through here, so the two can
/// never drift apart.
/// </summary>
public static class TripStatusRules
{
    /// <summary>The seat status a trip-wide move puts every eligible rider at.</summary>
    public static BookingStatus? BookingStatusFor(TripStatus tripStatus) => tripStatus switch
    {
        // EnRoute is deliberately absent: setting off moves no rider's seat. A
        // rider is still waiting at their kerb whether or not the car has left.
        TripStatus.Arrived => BookingStatus.Arrived,
        TripStatus.Active => BookingStatus.InProgress,
        TripStatus.Completed => BookingStatus.Completed,
        _ => null,
    };

    /// <summary>
    /// Where the trip stands given its bookings.
    ///
    /// <paramref name="current"/> matters for two cases the seats alone cannot
    /// answer: a finished or cancelled trip is terminal and never re-derived,
    /// and a journey whose last rider cancelled mid-ride stays where it was
    /// rather than falling back to Posted — the driver is still on the road.
    /// <paramref name="seatsLeft"/> only separates Posted from Full.
    /// </summary>
    public static TripStatus Derive(
        TripStatus current,
        IReadOnlyCollection<BookingStatus> bookings,
        int seatsLeft)
    {
        if (current is TripStatus.Completed or TripStatus.Cancelled) return current;

        var live = bookings.Where(BookingStatusRules.IsLive).ToList();

        // Someone aboard outranks someone still waiting: a trip with one rider
        // in the car is under way even if it has yet to reach the next pickup.
        if (live.Any(s => s == BookingStatus.InProgress)) return TripStatus.Active;
        if (live.Any(s => s == BookingStatus.Arrived)) return TripStatus.Arrived;

        // Nobody left holding a seat and at least one rider was delivered — the
        // last drop-off finishes the trip without the driver having to say so.
        if (live.Count == 0 && bookings.Any(s => s == BookingStatus.Completed))
            return TripStatus.Completed;

        // EnRoute joins these two as a state derivation cannot reach: no seat has
        // moved, so the seats say "Posted" while the driver is already driving.
        // Falling back to Posted here would put a departed trip back in search.
        if (current is TripStatus.Arrived or TripStatus.Active or TripStatus.EnRoute) return current;

        // Not Full. Capacity left the lifecycle in v2 (see Trip.IsFull): a trip
        // with no seats left is still Posted, and seatsLeft is the only thing
        // that says so. The parameter stays for the callers' sake and because
        // legacy rows still carry Full.
        _ = seatsLeft;
        return TripStatus.Posted;
    }

    /// <summary>
    /// The trip has not set off: seats can still be taken and given back, and
    /// every "can this still change" question upstream means this one.
    ///
    /// <see cref="TripStatus.Full"/> is included for rows written before
    /// capacity left the lifecycle. Nothing writes it any more — the migration
    /// rewrote what it could — but a status check that forgets it would strand
    /// any that survive.
    /// </summary>
    public static bool IsOpenForSeats(TripStatus status) =>
        status is TripStatus.Posted or TripStatus.Full;
}
