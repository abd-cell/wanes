using Wanes.Areas.Services.Users.Availability.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Users.Availability;

/// <summary>
/// The one authority on whether a rider can take another seat, so search's time
/// picker, booking a seat and joining a rider-posted trip all answer the
/// question the same way. Rules live in
/// <see cref="Wanes.Areas.Domain.Bookings.RiderAvailabilityRules"/>.
/// </summary>
[ScopedInjectable]
public interface IRiderAvailabilityService
{
    /// <summary>
    /// The caller's own diary, for a client that has to show which departures
    /// are open before one is chosen. Same rule as <see cref="CheckCanRide"/>,
    /// handed over as data instead of a verdict on one instant.
    ///
    /// <paramref name="ignoreTripId"/> excludes one trip, so a rider changing the
    /// trip they are already on is not greyed out by their own departure.
    /// </summary>
    Task<BaseResponse<RiderAvailabilityOutput>> GetMySchedule(int? ignoreTripId = null);

    /// <summary>
    /// May this rider take a seat departing at <paramref name="departAt"/>?
    /// Returns the error to hand back, or <c>null</c> when they are free.
    /// <paramref name="ignoreTripId"/> excludes a trip from its own clash check.
    /// </summary>
    Task<ErrorCode?> CheckCanRide(int riderId, DateTime departAt, int? ignoreTripId = null);
}
