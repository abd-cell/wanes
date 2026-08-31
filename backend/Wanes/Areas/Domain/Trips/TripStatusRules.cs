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

        if (current is TripStatus.Arrived or TripStatus.Active) return current;

        return seatsLeft <= 0 ? TripStatus.Full : TripStatus.Posted;
    }
}
