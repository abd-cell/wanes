using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Trips;
using Wanes.DataAccess.Repositories;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Users.Availability;

public class DriverAvailabilityService : IDriverAvailabilityService
{
    private readonly IRepository<Trip> tripRepository;

    public DriverAvailabilityService(IRepository<Trip> tripRepository)
    {
        this.tripRepository = tripRepository;
    }

    public async Task<bool> IsEngaged(int driverId) =>
        await tripRepository
            .Where(t => t.DriverId == driverId && !t.IsDeleted
                        && (t.Status == TripStatus.Arrived || t.Status == TripStatus.Active))
            .AnyAsync();

    public async Task<ErrorCode?> CheckCanCommit(int driverId, DateTime departAt, int? ignoreTripId = null)
    {
        // Everything still on the driver's plate, in one read: the journey they
        // may be on and the departures they have already promised.
        var held = await tripRepository
            .Where(t => t.DriverId == driverId && !t.IsDeleted
                        && (ignoreTripId == null || t.Id != ignoreTripId)
                        && (t.Status == TripStatus.Posted || t.Status == TripStatus.Full
                            || t.Status == TripStatus.Arrived || t.Status == TripStatus.Active))
            .Select(t => new { t.Status, t.DepartAt })
            .ToListAsync();

        // Being on the road outranks a scheduling clash: it is the more precise
        // reason, and the only one the driver can do nothing about but finish.
        if (held.Any(t => DriverAvailabilityRules.IsEngaged(t.Status)))
            return ErrorCode.DriverOnActiveTrip;

        if (held.Any(t => DriverAvailabilityRules.Clashes(t.DepartAt, departAt)))
            return ErrorCode.DriverTripTimeConflict;

        return null;
    }

    public async Task<List<DateTime>> CommittedDepartures(int driverId) =>
        await tripRepository
            .Where(t => t.DriverId == driverId && !t.IsDeleted
                        && (t.Status == TripStatus.Posted || t.Status == TripStatus.Full
                            || t.Status == TripStatus.Arrived || t.Status == TripStatus.Active))
            .Select(t => t.DepartAt)
            .ToListAsync();

    public async Task<HashSet<int>> BusyDrivers(IReadOnlyCollection<int> driverIds, DateTime departAt)
    {
        if (driverIds.Count == 0) return [];

        var ids = driverIds.ToList();   // a List translates to IN (…); the interface may not
        var held = await tripRepository
            .Where(t => ids.Contains(t.DriverId) && !t.IsDeleted
                        && (t.Status == TripStatus.Posted || t.Status == TripStatus.Full
                            || t.Status == TripStatus.Arrived || t.Status == TripStatus.Active))
            .Select(t => new { t.DriverId, t.Status, t.DepartAt })
            .ToListAsync();

        return held
            .Where(t => DriverAvailabilityRules.IsEngaged(t.Status)
                        || DriverAvailabilityRules.Clashes(t.DepartAt, departAt))
            .Select(t => t.DriverId)
            .ToHashSet();
    }
}
