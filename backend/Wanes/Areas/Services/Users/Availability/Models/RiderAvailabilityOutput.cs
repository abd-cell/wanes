namespace Wanes.Areas.Services.Users.Availability.Models;

/// <summary>
/// Everything a client needs to grey out the departures this rider cannot take
/// a seat at, rather than letting them pick one and reading the refusal
/// afterwards. The rider-side twin of <see cref="DriverAvailabilityOutput"/>,
/// and the same reasoning applies to the window travelling with the list: the
/// clash rule is
/// <see cref="Wanes.Areas.Domain.Bookings.RiderAvailabilityRules.ClashWindow"/>,
/// and a client carrying its own copy of the number is a client that will one
/// day offer a slot the API refuses.
/// </summary>
public class RiderAvailabilityOutput
{
    /// <summary>Departures the rider already holds a seat (or a hold) for, in UTC.</summary>
    public List<DateTime> CommittedDepartures { get; set; } = [];

    /// <summary>How close to a committed departure another one may not be.</summary>
    public int ClashWindowMinutes { get; set; }

    /// <summary>
    /// The rider is on a ride right now — in the car, or the driver is at their
    /// kerb waiting. A separate flag because it is a different sentence: not
    /// "not then" but "not yet".
    /// </summary>
    public bool IsEngaged { get; set; }
}
