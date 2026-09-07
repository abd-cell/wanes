using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Domain.Trips;

/// <summary>
/// When a driver may take on another ride. One driver drives one car, so the
/// two ways they can be unavailable are physical: they are on the road right
/// now, or they are already promised to a trip leaving at about the same time.
///
/// Every place that targets, offers or commits a driver reads from here —
/// hail targeting, the driver's own hail list, accepting a hail, posting a trip
/// and the presence flag — because a driver who is filtered out of the push but
/// still allowed to accept has simply moved the double-booking one tap later.
/// </summary>
public static class DriverAvailabilityRules
{
    /// <summary>
    /// The driver is out driving: on their way to a pickup, at one, or carrying
    /// riders. Posted and Full are promises, not journeys, and so are not
    /// "engaged" — the driver is still at home with the trip in their diary.
    /// </summary>
    public static bool IsEngaged(TripStatus status) =>
        status is TripStatus.EnRoute or TripStatus.Arrived or TripStatus.Active;

    /// <summary>
    /// The trip still holds a slot in the driver's day — anything not finished
    /// and not called off.
    /// </summary>
    public static bool HoldsSchedule(TripStatus status) =>
        status is TripStatus.Posted or TripStatus.Full
            or TripStatus.EnRoute or TripStatus.Arrived or TripStatus.Active;

    /// <summary>
    /// How close two of one driver's departures may be. Taken from the matching
    /// envelope rather than restated: a rider searching at T is offered trips
    /// leaving within <see cref="MatchRules.TimeWindow"/> of T, so two trips that
    /// close together are competing for the same driver at the same moment.
    /// </summary>
    public static readonly TimeSpan ClashWindow = MatchRules.TimeWindow;

    /// <summary>Whether two departures are too close together for one driver.</summary>
    public static bool Clashes(DateTime a, DateTime b) => (a - b).Duration() < ClashWindow;
}
