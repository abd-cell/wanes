using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Users.Availability;

/// <summary>
/// The one authority on whether a driver can take another ride right now, so
/// hail targeting, the driver's hail list, accepting a hail and posting a trip
/// all answer the question the same way. Rules live in
/// <see cref="Wanes.Areas.Domain.Trips.DriverAvailabilityRules"/>.
/// </summary>
[ScopedInjectable]
public interface IDriverAvailabilityService
{
    /// <summary>The driver is out on a trip right now (at a pickup, or carrying riders).</summary>
    Task<bool> IsEngaged(int driverId);

    /// <summary>
    /// May this driver commit to a trip departing at <paramref name="departAt"/>?
    /// Returns the error to hand back, or <c>null</c> when they are free.
    /// <paramref name="ignoreTripId"/> excludes the trip being edited from its
    /// own clash check.
    /// </summary>
    Task<ErrorCode?> CheckCanCommit(int driverId, DateTime departAt, int? ignoreTripId = null);

    /// <summary>
    /// Every departure this driver is already promised to — the trips that still
    /// hold a slot in their day.
    ///
    /// For callers that have to answer the clash question about many different
    /// departures at once, which the driver's hail list does: each hail carries
    /// its own wanted departure, so one <see cref="CheckCanCommit"/> against a
    /// single moment cannot filter the list.
    /// </summary>
    Task<List<DateTime>> CommittedDepartures(int driverId);

    /// <summary>
    /// Of <paramref name="driverIds"/>, those who must not be offered a ride
    /// departing at <paramref name="departAt"/>. One query for the whole set —
    /// hail targeting runs this over every candidate the radius returned.
    /// </summary>
    Task<HashSet<int>> BusyDrivers(IReadOnlyCollection<int> driverIds, DateTime departAt);
}
