using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Schedules;
using Wanes.Areas.Domain.Series;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Bookings.Models;
using Wanes.Areas.Services.RideRequests.Models;
using Wanes.Areas.Services.Series.Models;
using Wanes.Areas.Services.Trips.Models;
using Wanes.DataAccess.Repositories;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Series;

/// <summary>
/// Reads the recurrence behind a batch of rows in a handful of queries, so a
/// board of forty cards costs the same as one.
/// </summary>
public class SeriesInfoService : ISeriesInfoService
{
    private readonly ISecurityManager securityManager;
    private readonly IRepository<TripSchedule> scheduleRepository;
    private readonly IRepository<SeriesCommitment> seriesRepository;
    private readonly IRepository<RideRequest> requestRepository;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<User> userRepository;

    public SeriesInfoService(
        ISecurityManager securityManager,
        IRepository<TripSchedule> scheduleRepository,
        IRepository<SeriesCommitment> seriesRepository,
        IRepository<RideRequest> requestRepository,
        IRepository<Trip> tripRepository,
        IRepository<User> userRepository)
    {
        this.securityManager = securityManager;
        this.scheduleRepository = scheduleRepository;
        this.seriesRepository = seriesRepository;
        this.requestRepository = requestRepository;
        this.tripRepository = tripRepository;
        this.userRepository = userRepository;
    }

    public async Task Decorate(IReadOnlyCollection<RideRequestRow> rows)
    {
        var infos = await Describe(rows.Select(r => r.ScheduleId));
        foreach (var row in rows)
            if (row.ScheduleId is { } id && infos.TryGetValue(id, out var info)) row.Series = info;
    }

    public async Task Decorate(IReadOnlyCollection<TripOutput> rows)
    {
        var commitments = await CommitmentSchedules(rows.Select(r => r.SeriesCommitmentId));
        var infos = await Describe(
            rows.Select(r => r.ScheduleId ?? Lookup(commitments, r.SeriesCommitmentId)));
        foreach (var row in rows)
        {
            var id = row.ScheduleId ?? Lookup(commitments, row.SeriesCommitmentId);
            if (id != null && infos.TryGetValue(id.Value, out var info))
                row.Series = WithCommitment(info, row.SeriesCommitmentId);
        }
    }

    public async Task Decorate(IReadOnlyCollection<BookingOutput> rows)
    {
        var commitments = await CommitmentSchedules(
            rows.Select(r => r.SeriesCommitmentId).Concat(rows.Select(r => r.TripSeriesCommitmentId)));
        var infos = await Describe(
            rows.Select(r => r.ScheduleId ?? Lookup(commitments, r.TripSeriesCommitmentId)));
        foreach (var row in rows)
        {
            var id = row.ScheduleId ?? Lookup(commitments, row.TripSeriesCommitmentId);
            if (id != null && infos.TryGetValue(id.Value, out var info))
                row.Series = WithCommitment(info, row.SeriesCommitmentId ?? row.TripSeriesCommitmentId);
        }
    }

    private static SeriesInfo WithCommitment(SeriesInfo info, int? commitmentId) => new()
    {
        ScheduleId = info.ScheduleId,
        OwnerRole = info.OwnerRole,
        Recurrence = info.Recurrence,
        DaysOfWeek = info.DaysOfWeek,
        DayOfMonth = info.DayOfMonth,
        TimeOfDay = info.TimeOfDay,
        StartDate = info.StartDate,
        EndDate = info.EndDate,
        IsPaused = info.IsPaused,
        UpcomingDays = info.UpcomingDays,
        HasDriver = info.HasDriver,
        DriverName = info.DriverName,
        ProposalCount = info.ProposalCount,
        MySeriesId = info.MySeriesId,
        MySeriesStatus = info.MySeriesStatus,
        CommitmentId = commitmentId,
    };

    private static int? Lookup(IReadOnlyDictionary<int, int> map, int? commitmentId) =>
        commitmentId is { } id && map.TryGetValue(id, out var scheduleId) ? scheduleId : null;

    /// <summary>commitment id → schedule id.</summary>
    private async Task<Dictionary<int, int>> CommitmentSchedules(IEnumerable<int?> ids)
    {
        var wanted = ids.Where(i => i != null).Select(i => i!.Value).Distinct().ToList();
        if (wanted.Count == 0) return [];
        return await seriesRepository.Query(includeDeleted: true)
            .Where(c => wanted.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.ScheduleId);
    }

    private async Task<Dictionary<int, SeriesInfo>> Describe(IEnumerable<int?> scheduleIds)
    {
        var ids = scheduleIds.Where(i => i != null).Select(i => i!.Value).Distinct().ToList();
        if (ids.Count == 0) return [];

        var callerId = securityManager.UserId ?? 0;
        var now = DateTime.UtcNow;

        var schedules = await scheduleRepository.Query(includeDeleted: true)
            .Where(s => ids.Contains(s.Id))
            .ToListAsync();

        var live = await seriesRepository
            .Where(c => ids.Contains(c.ScheduleId)
                        && (c.Status == SeriesStatus.Proposed || c.Status == SeriesStatus.Active))
            .ToListAsync();

        var driverIds = live.Where(c => c.Side == SeriesSide.DriverServes && c.Status == SeriesStatus.Active)
            .Select(c => c.DriverId).Distinct().ToList();
        var drivers = driverIds.Count == 0
            ? []
            : await userRepository.Where(u => driverIds.Contains(u.Id)).ToListAsync();

        var riderIds = schedules.Where(s => s.OwnerRole == ActiveRole.Rider).Select(s => s.Id).ToList();
        var driverSchedIds = schedules.Where(s => s.OwnerRole == ActiveRole.Driver).Select(s => s.Id).ToList();

        var openRequests = riderIds.Count == 0
            ? []
            : await requestRepository
                .Where(r => r.ScheduleId != null && riderIds.Contains(r.ScheduleId.Value)
                            && r.Status == RideRequestStatus.Open && r.DepartAt > now)
                .Select(r => r.ScheduleId!.Value)
                .ToListAsync();
        var openTrips = driverSchedIds.Count == 0
            ? []
            : await tripRepository
                .Where(t => t.ScheduleId != null && driverSchedIds.Contains(t.ScheduleId.Value)
                            && (t.Status == TripStatus.Posted || t.Status == TripStatus.Full)
                            && t.DepartAt > now)
                .Select(t => t.ScheduleId!.Value)
                .ToListAsync();

        var result = new Dictionary<int, SeriesInfo>();
        foreach (var s in schedules)
        {
            var onIt = live.Where(c => c.ScheduleId == s.Id).ToList();
            var driving = onIt.FirstOrDefault(c => c.Side == SeriesSide.DriverServes && c.Status == SeriesStatus.Active);
            var mine = onIt.FirstOrDefault(c => callerId != 0 && c.CommitterId == callerId);

            result[s.Id] = new SeriesInfo
            {
                ScheduleId = s.Id,
                OwnerRole = s.OwnerRole,
                Recurrence = s.Recurrence,
                DaysOfWeek = s.DaysOfWeek,
                DayOfMonth = s.DayOfMonth,
                TimeOfDay = s.TimeOfDay,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                IsPaused = s.IsPaused || s.IsDeleted,
                UpcomingDays = s.OwnerRole == ActiveRole.Rider
                    ? openRequests.Count(x => x == s.Id)
                    : openTrips.Count(x => x == s.Id),
                HasDriver = driving != null,
                DriverName = driving == null ? null : drivers.FirstOrDefault(d => d.Id == driving.DriverId) is { } d
                    ? d.DisplayName ?? d.FirstName
                    : null,
                ProposalCount = onIt.Count(c => c.Status == SeriesStatus.Proposed),
                MySeriesId = mine?.Id,
                MySeriesStatus = mine?.Status,
            };
        }
        return result;
    }
}
