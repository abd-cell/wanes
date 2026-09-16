using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Services.Users.Availability.Models;
using Wanes.DataAccess.Repositories;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Users.Availability;

public class DriverAvailabilityService : IDriverAvailabilityService
{
    private readonly IRepository<Trip> tripRepository;
    private readonly ISecurityManager securityManager;

    public DriverAvailabilityService(IRepository<Trip> tripRepository, ISecurityManager securityManager)
    {
        this.tripRepository = tripRepository;
        this.securityManager = securityManager;
    }

    public async Task<bool> IsEngaged(int driverId) =>
        await tripRepository
            .Where(t => t.DriverId == driverId && !t.IsDeleted
                        && DriverAvailabilityRules.EngagedStatuses.Contains(t.Status))
            .AnyAsync();

    public async Task<ErrorCode?> CheckCanCommit(int driverId, DateTime departAt, int? ignoreTripId = null)
    {
        // Everything still on the driver's plate, in one read: the journey they
        // may be on and the departures they have already promised.
        var held = await tripRepository
            .Where(t => t.DriverId == driverId && !t.IsDeleted
                        && (ignoreTripId == null || t.Id != ignoreTripId)
                        && DriverAvailabilityRules.HoldingStatuses.Contains(t.Status))
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
                        && DriverAvailabilityRules.HoldingStatuses.Contains(t.Status))
            .Select(t => t.DepartAt)
            .ToListAsync();

    public async Task<BaseResponse<DriverAvailabilityOutput>> GetMySchedule(int? ignoreTripId = null)
    {
        var driverId = securityManager.RequireUserId();

        var held = await tripRepository
            .Where(t => t.DriverId == driverId && !t.IsDeleted
                        && (ignoreTripId == null || t.Id != ignoreTripId)
                        && DriverAvailabilityRules.HoldingStatuses.Contains(t.Status))
            .Select(t => new { t.Status, t.DepartAt })
            .ToListAsync();

        return new BaseResponse<DriverAvailabilityOutput>(new DriverAvailabilityOutput
        {
            CommittedDepartures = held.Select(t => t.DepartAt).OrderBy(d => d).ToList(),
            ClashWindowMinutes = (int)DriverAvailabilityRules.ClashWindow.TotalMinutes,
            IsEngaged = held.Any(t => DriverAvailabilityRules.IsEngaged(t.Status)),
        });
    }

    public async Task<HashSet<int>> BusyDrivers(IReadOnlyCollection<int> driverIds, DateTime departAt)
    {
        if (driverIds.Count == 0) return [];

        var ids = driverIds.ToList();   // a List translates to IN (…); the interface may not
        // A trip nobody is driving commits nobody, so it cannot make a driver
        // busy — not even the rider who wrote it, who is busy by their seat on
        // it rather than by the trip itself.
        var held = await tripRepository
            .Where(t => t.DriverId != null && ids.Contains(t.DriverId!.Value) && !t.IsDeleted
                        && DriverAvailabilityRules.HoldingStatuses.Contains(t.Status))
            .Select(t => new { DriverId = t.DriverId!.Value, t.Status, t.DepartAt })
            .ToListAsync();

        return held
            .Where(t => DriverAvailabilityRules.IsEngaged(t.Status)
                        || DriverAvailabilityRules.Clashes(t.DepartAt, departAt))
            .Select(t => t.DriverId)
            .ToHashSet();
    }
}
