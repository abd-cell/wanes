using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Services.Users.Availability.Models;
using Wanes.DataAccess.Repositories;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Users.Availability;

public class RiderAvailabilityService : IRiderAvailabilityService
{
    private readonly IRepository<Booking> bookingRepository;
    private readonly IRepository<Trip> tripRepository;
    private readonly ISecurityManager securityManager;

    public RiderAvailabilityService(
        IRepository<Booking> bookingRepository,
        IRepository<Trip> tripRepository,
        ISecurityManager securityManager)
    {
        this.bookingRepository = bookingRepository;
        this.tripRepository = tripRepository;
        this.securityManager = securityManager;
    }

    public async Task<ErrorCode?> CheckCanRide(int riderId, DateTime departAt, int? ignoreTripId = null)
    {
        var held = await Held(riderId, ignoreTripId);

        // Being on a ride outranks a scheduling clash: it is the more precise
        // reason, and the only one the rider can do nothing about but arrive.
        if (held.Any(h => h.Engaged)) return ErrorCode.RiderOnActiveTrip;

        if (held.Any(h => RiderAvailabilityRules.Clashes(h.DepartAt, departAt)))
            return ErrorCode.RiderSeatTimeConflict;

        return null;
    }

    public async Task<BaseResponse<RiderAvailabilityOutput>> GetMySchedule(int? ignoreTripId = null)
    {
        var riderId = securityManager.RequireUserId();
        var held = await Held(riderId, ignoreTripId);

        return new BaseResponse<RiderAvailabilityOutput>(new RiderAvailabilityOutput
        {
            CommittedDepartures = held.Select(h => h.DepartAt).OrderBy(d => d).ToList(),
            ClashWindowMinutes = (int)RiderAvailabilityRules.ClashWindow.TotalMinutes,
            IsEngaged = held.Any(h => h.Engaged),
        });
    }

    /// <summary>
    /// Everything still on the rider's plate — every seat they hold, on a trip
    /// somebody is driving or on one still waiting for a driver.
    ///
    /// One kind of row now covers both, because a trip a rider posted *is* a
    /// trip: a seat on it is an ordinary <c>Pending</c> booking, and
    /// <see cref="RiderAvailabilityRules.HoldingStatuses"/> already counts
    /// Pending. What used to be four reads over two tables is two reads over
    /// one, and the clash rule cannot disagree with itself any more.
    ///
    /// The departure is fetched by trip id rather than navigated to: a
    /// navigation property is populated by an Include the caller has to
    /// remember, and one that forgets gets an empty answer rather than an error.
    /// A trip already cancelled — including one nobody took in time — is dropped,
    /// because it holds nothing.
    /// </summary>
    private async Task<List<(DateTime DepartAt, bool Engaged)>> Held(int riderId, int? ignoreTripId)
    {
        var seats = await bookingRepository
            .Where(b => b.RiderId == riderId
                        && (ignoreTripId == null || b.TripId != ignoreTripId)
                        && RiderAvailabilityRules.HoldingStatuses.Contains(b.Status))
            .Select(b => new { b.TripId, b.Status })
            .ToListAsync();

        var tripIds = seats.Select(s => s.TripId).Distinct().ToList();
        var departures = tripIds.Count == 0
            ? []
            : await tripRepository
                .Where(t => tripIds.Contains(t.Id) && t.Status != TripStatus.Cancelled)
                .Select(t => new { t.Id, t.DepartAt })
                .ToListAsync();
        var departAtOf = departures.ToDictionary(t => t.Id, t => t.DepartAt);

        return
        [
            .. seats.Where(s => departAtOf.ContainsKey(s.TripId))
                    .Select(s => (departAtOf[s.TripId], RiderAvailabilityRules.IsEngaged(s.Status))),
        ];
    }
}
