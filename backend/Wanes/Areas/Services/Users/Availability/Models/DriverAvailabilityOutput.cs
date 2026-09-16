namespace Wanes.Areas.Services.Users.Availability.Models;

/// <summary>
/// Everything a client needs to grey out the departures this driver cannot
/// take, rather than letting them pick one and reading the refusal afterwards.
///
/// The window travels with the list on purpose. Whether two departures clash is
/// a platform rule
/// (<see cref="Wanes.Areas.Domain.Trips.DriverAvailabilityRules.ClashWindow"/>),
/// and a client carrying its own copy of the number is a client that will one
/// day offer a slot the API refuses.
/// </summary>
public class DriverAvailabilityOutput
{
    /// <summary>Departures already promised, in UTC.</summary>
    public List<DateTime> CommittedDepartures { get; set; } = [];

    /// <summary>How close to a committed departure another one may not be.</summary>
    public int ClashWindowMinutes { get; set; }

    /// <summary>
    /// The driver is out on a trip right now, so no departure at all is
    /// available until they finish. A separate flag because it is a different
    /// sentence to the driver: not "not then" but "not yet".
    /// </summary>
    public bool IsEngaged { get; set; }
}
