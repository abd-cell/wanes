using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.Series;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Configuration.Models;
using Wanes.Areas.Services.Marketplace.Models;
using Wanes.Areas.Services.Notifications;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Marketplace;

/// <summary>
/// The server's record of what a user agreed to. The app keeps a copy to skip
/// the sheet it has already shown; this is the copy that answers a dispute.
/// </summary>
public class AcknowledgementService : IAcknowledgementService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly IRepository<UserAcknowledgement> repository;

    public AcknowledgementService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        IRepository<UserAcknowledgement> repository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.repository = repository;
    }

    public async Task<BaseResponse<List<AcknowledgementOutput>>> Mine()
    {
        var userId = securityManager.RequireUserId();
        var rows = await repository.Where(a => a.UserId == userId)
            .OrderBy(a => a.Kind).ThenBy(a => a.Version)
            .ToListAsync();
        return new BaseResponse<List<AcknowledgementOutput>>(rows.Select(a => new AcknowledgementOutput(a)).ToList());
    }

    public async Task<BaseResponse<AcknowledgementOutput>> Record(AcknowledgementInput input)
    {
        var userId = securityManager.RequireUserId();
        var existing = repository.FirstOrDefault(a =>
            a.UserId == userId && a.Kind == input.Kind && a.Version == input.Version);
        if (existing != null) return new BaseResponse<AcknowledgementOutput>(new AcknowledgementOutput(existing));

        var row = new UserAcknowledgement
        {
            UserId = userId,
            Kind = input.Kind,
            Version = input.Version,
            AcceptedAt = DateTime.UtcNow,
            CreatedBy = userId,
        };
        repository.Create(row);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.AcknowledgementRecord, nameof(UserAcknowledgement), row.Id);
        return new BaseResponse<AcknowledgementOutput>(new AcknowledgementOutput(row));
    }
}

/// <summary>
/// Route alerts and request watches: the driver's way of hearing about demand
/// that is worth planning a run around, without watching the board all day.
/// </summary>
public class DemandAlertService : IDemandAlertService
{
    /// <summary>How many standing route alerts one driver may keep.</summary>
    public const int MaxAlertsPerDriver = 20;

    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IRepository<DemandAlert> alertRepository;
    private readonly IRepository<DemandAlertHit> hitRepository;
    private readonly IRepository<RideRequest> requestRepository;
    private readonly IRepository<RideRequestParticipant> participantRepository;
    private readonly IRepository<User> userRepository;

    public DemandAlertService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IRepository<DemandAlert> alertRepository,
        IRepository<DemandAlertHit> hitRepository,
        IRepository<RideRequest> requestRepository,
        IRepository<RideRequestParticipant> participantRepository,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.alertRepository = alertRepository;
        this.hitRepository = hitRepository;
        this.requestRepository = requestRepository;
        this.participantRepository = participantRepository;
        this.userRepository = userRepository;
    }

    public async Task<BaseResponse<List<DemandAlertOutput>>> Mine()
    {
        var driverId = securityManager.RequireUserId();
        var rows = await alertRepository.Where(a => a.DriverId == driverId && a.IsActive)
            .OrderByDescending(a => a.Id)
            .ToListAsync();
        return new BaseResponse<List<DemandAlertOutput>>(rows.Select(a => new DemandAlertOutput(a)).ToList());
    }

    public async Task<BaseResponse<DemandAlertOutput>> Create(DemandAlertInput input)
    {
        var driverId = securityManager.RequireUserId();
        var driver = await userRepository.GetByIdAsync(driverId);
        if (driver == null) return new BaseResponse<DemandAlertOutput>(default, ErrorCode.NotFound);
        if (!driver.IsDriver) return new BaseResponse<DemandAlertOutput>(default, ErrorCode.Forbidden);

        var minSeats = Math.Clamp(input.MinSeats, 1, RiderTripRules.MaxSeats);
        DemandAlert alert;
        RideRequest? watched = null;

        if (input.RideRequestId is { } requestId)
        {
            watched = requestRepository.FirstOrDefault(r => r.Id == requestId);
            if (watched == null) return new BaseResponse<DemandAlertOutput>(default, ErrorCode.RideRequestNotFound);
            if (!watched.IsOpenAt(DateTime.UtcNow))
                return new BaseResponse<DemandAlertOutput>(default, ErrorCode.RideRequestNotOpen);

            // Watching the same request twice is the same watch.
            var same = alertRepository.FirstOrDefault(a =>
                a.DriverId == driverId && a.RideRequestId == requestId && a.IsActive);
            if (same != null)
            {
                same.MinSeats = minSeats;
                alertRepository.Update(same);
                await unitOfWork.SaveAsync();
                await Match(watched);
                return new BaseResponse<DemandAlertOutput>(new DemandAlertOutput(same));
            }

            alert = new DemandAlert
            {
                DriverId = driverId,
                RideRequestId = requestId,
                OriginAddress = watched.OriginAddress,
                Origin = watched.Origin,
                DestinationAddress = watched.DestinationAddress,
                Destination = watched.Destination,
                RadiusMeters = 500,
                MinSeats = minSeats,
            };
        }
        else
        {
            if (input.Origin == null || input.Destination == null)
                return new BaseResponse<DemandAlertOutput>(default, ErrorCode.ValidationError);

            var standing = await alertRepository.CountAsync(a =>
                a.DriverId == driverId && a.IsActive && a.RideRequestId == null);
            if (standing >= MaxAlertsPerDriver)
                return new BaseResponse<DemandAlertOutput>(default, ErrorCode.Conflict);

            alert = new DemandAlert
            {
                DriverId = driverId,
                OriginAddress = input.Origin.Address,
                Origin = GeoFactory.Point(input.Origin.Lat, input.Origin.Lng),
                DestinationAddress = input.Destination.Address,
                Destination = GeoFactory.Point(input.Destination.Lat, input.Destination.Lng),
                RadiusMeters = input.RadiusMeters,
                MinSeats = minSeats,
                RecurringOnly = input.RecurringOnly,
            };
        }

        alert.CreatedBy = driverId;
        alertRepository.Create(alert);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.DemandAlertCreate, nameof(DemandAlert), alert.Id);

        // A watch on a request that is already full enough fires at once.
        if (watched != null) await Match(watched);

        return new BaseResponse<DemandAlertOutput>(new DemandAlertOutput(alert));
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var driverId = securityManager.RequireUserId();
        var alert = alertRepository.FirstOrDefault(a => a.Id == id && a.DriverId == driverId);
        if (alert == null) return new BaseResponse(ErrorCode.DemandAlertNotFound);

        alert.IsActive = false;
        alertRepository.SoftDelete(alert);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.DemandAlertDelete, nameof(DemandAlert), alert.Id);
        return new BaseResponse();
    }

    public async Task<int> Match(RideRequest request)
    {
        try
        {
            var now = DateTime.UtcNow;
            if (!request.IsOpenAt(now)) return 0;

            var candidates = await alertRepository
                .Where(a => a.IsActive
                            && a.MinSeats <= request.SeatsRequested
                            && (a.RideRequestId == null || a.RideRequestId == request.Id))
                .ToListAsync();
            if (candidates.Count == 0) return 0;

            // Straight-line distance in memory: the alert table is small, and
            // the same arithmetic runs identically over the test doubles.
            var matching = candidates.Where(a =>
                    a.RideRequestId == request.Id
                    || ((!a.RecurringOnly || request.ScheduleId != null)
                        && GeoDistance.Km(a.Origin, request.Origin) * 1000 <= a.RadiusMeters
                        && GeoDistance.Km(a.Destination, request.Destination) * 1000 <= a.RadiusMeters))
                .ToList();
            if (matching.Count == 0) return 0;

            var alertIds = matching.Select(a => a.Id).ToList();
            var fired = await hitRepository
                .Where(h => h.RideRequestId == request.Id && alertIds.Contains(h.DemandAlertId))
                .Select(h => h.DemandAlertId)
                .ToListAsync();

            var aboard = await participantRepository
                .Where(p => p.RideRequestId == request.Id && p.Status == RideRequestParticipantStatus.Active)
                .Select(p => p.RiderId)
                .ToListAsync();

            var driverIds = matching.Select(a => a.DriverId).Distinct().ToList();
            var drivers = await userRepository
                .Where(u => driverIds.Contains(u.Id)
                            && u.IsDriver
                            && !u.IsDisabled
                            && u.DriverStatus == DriverStatus.Verified)
                .ToListAsync();

            var toTell = new HashSet<int>();
            foreach (var alert in matching)
            {
                if (fired.Contains(alert.Id) || aboard.Contains(alert.DriverId)) continue;
                var driver = drivers.FirstOrDefault(d => d.Id == alert.DriverId);
                if (driver == null) continue;
                if (RiderEligibilityRules.CheckDriver(driver, request.DriverGenderPolicy) != null) continue;

                hitRepository.Create(new DemandAlertHit { DemandAlertId = alert.Id, RideRequestId = request.Id });
                alert.LastNotifiedAt = now;
                alert.NotifiedCount++;
                // A watch on one request has done its job once it fires.
                if (alert.RideRequestId != null) alert.IsActive = false;
                alertRepository.Update(alert);
                toTell.Add(alert.DriverId);
            }
            if (toTell.Count == 0) return 0;

            await unitOfWork.SaveAsync();
            await notificationService.NotifyMany(toTell, NotificationTemplate.DemandAlertMatchedDriver,
                args: new
                {
                    seats = request.SeatsRequested,
                    origin = request.OriginAddress,
                    destination = request.DestinationAddress,
                },
                data: new { rideRequestId = request.Id, seats = request.SeatsRequested });
            return toTell.Count;
        }
        catch (DbUpdateException)
        {
            // Two joins racing to fire the same alert: the unique hit index
            // lets one through. Best-effort by design.
            unitOfWork.Detach();
            return 0;
        }
    }

    public async Task<BaseResponse<PageOutput<DemandAlertOutput>>> List(PageInput page, int? driverId)
    {
        IQueryable<DemandAlert> query = alertRepository.Query().Include(a => a.Driver);
        if (driverId != null) query = query.Where(a => a.DriverId == driverId);
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(a => a.OriginAddress.Contains(term) || a.DestinationAddress.Contains(term));
        }

        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(a => a.Id).Paginate(page).ToListAsync();
        return new BaseResponse<PageOutput<DemandAlertOutput>>(new PageOutput<DemandAlertOutput>
        {
            TotalRows = total,
            Data = rows.Select(a => new DemandAlertOutput(a)).ToList(),
        });
    }
}

/// <summary>
/// The reliability record — see <see cref="ReliabilityRules"/> for what things
/// cost and why.
/// </summary>
public class ReliabilityService : IReliabilityService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IAppConfigurationService appConfigurationService;
    private readonly IRepository<ReliabilityEvent> eventRepository;
    private readonly IRepository<User> userRepository;

    public ReliabilityService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IAppConfigurationService appConfigurationService,
        IRepository<ReliabilityEvent> eventRepository,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.appConfigurationService = appConfigurationService;
        this.eventRepository = eventRepository;
        this.userRepository = userRepository;
    }

    public async Task<CancelPreviewOutput> PreviewDriverCancel(Trip trip, int ridersAffected, bool ridersRequeued)
    {
        var settings = await Settings();
        var now = DateTime.UtcNow;
        var kind = await Classify(trip, ridersAffected, now, settings);
        var points = ReliabilityRules.PointsFor(kind);
        var current = trip.DriverId is { } driverId
            ? await PointsInWindow(driverId, ActiveRole.Driver, now, settings)
            : 0;

        return new CancelPreviewOutput
        {
            Kind = kind,
            Points = points,
            RidersAffected = ridersAffected,
            ReasonRequired = ReliabilityRules.ReasonRequired(ridersAffected),
            PointsAfter = current + points,
            WarnPoints = settings.ReliabilityWarnPoints,
            SuspendPoints = settings.ReliabilitySuspendPoints,
            WindowDays = settings.ReliabilityWindowDays,
            WouldSuspend = points > 0 && current + points >= settings.ReliabilitySuspendPoints,
            RidersRequeued = ridersRequeued,
        };
    }

    public async Task<ReliabilityEvent> RecordDriverCancel(Trip trip, int driverId, int ridersAffected,
        CancelReason? reason, string? note, TripStatus statusAtCancel)
    {
        var settings = await Settings();
        var now = DateTime.UtcNow;
        var kind = await Classify(trip, ridersAffected, now, settings, statusAtCancel);

        var entry = new ReliabilityEvent
        {
            UserId = driverId,
            Role = ActiveRole.Driver,
            Kind = kind,
            Points = ReliabilityRules.PointsFor(kind),
            TripId = trip.Id,
            Reason = reason,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            RidersAffected = ridersAffected,
            MinutesBeforeDeparture = (int)Math.Round((trip.DepartAt - now).TotalMinutes),
            NeedsReview = ReliabilityRules.NeedsReview(reason) && kind != ReliabilityEventKind.FreeCancel,
            CreatedBy = driverId,
        };
        eventRepository.Create(entry);

        var driver = await userRepository.GetByIdAsync(driverId);
        if (driver != null && ReliabilityRules.CountsAgainstCompletion(kind))
        {
            driver.DriverCancellations++;
            userRepository.Update(driver);
        }
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.ReliabilityRecord, nameof(ReliabilityEvent), entry.Id);

        if (driver != null && entry.Points > 0) await ApplyStanding(driver, now, settings);
        return entry;
    }

    public async Task RecordRiderCancel(Booking booking, Trip trip)
    {
        var settings = await Settings();
        var now = DateTime.UtcNow;
        // A seat booked with a series is a standing promise: the driver planned
        // the week around it, so giving it back needs the series notice.
        var late = booking.SeriesCommitmentId != null
            ? SeriesRules.IsShortNotice(now, trip.DepartAt, settings.SeriesSkipNoticeHours)
            : ReliabilityRules.IsLateRiderCancel(now, trip.DepartAt, settings.LateCancelLeadMinutes);
        if (!late) return;
        await RecordRider(booking, trip, ReliabilityEventKind.RiderLateCancel, now,
            u => u.RiderLateCancels++);
    }

    public async Task RecordSeriesEnd(int driverId, Trip trip, int ridersAffected, CancelReason? reason, string? note)
    {
        var settings = await Settings();
        var now = DateTime.UtcNow;
        const ReliabilityEventKind kind = ReliabilityEventKind.SeriesEndShortNotice;

        eventRepository.Create(new ReliabilityEvent
        {
            UserId = driverId,
            Role = ActiveRole.Driver,
            Kind = kind,
            Points = ReliabilityRules.PointsFor(kind),
            TripId = trip.Id,
            Reason = reason,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            RidersAffected = ridersAffected,
            MinutesBeforeDeparture = (int)Math.Round((trip.DepartAt - now).TotalMinutes),
            NeedsReview = ReliabilityRules.NeedsReview(reason),
            CreatedBy = driverId,
        });

        var driver = await userRepository.GetByIdAsync(driverId);
        if (driver != null)
        {
            driver.DriverCancellations++;
            userRepository.Update(driver);
        }
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.ReliabilityRecord, nameof(Trip), trip.Id);

        if (driver != null) await ApplyStanding(driver, now, settings);
    }

    public Task RecordRiderNoShow(Booking booking, Trip trip) =>
        RecordRider(booking, trip, ReliabilityEventKind.RiderNoShow, DateTime.UtcNow, u => u.RiderNoShows++);

    public ErrorCode? CheckCanTake(User driver, DateTime departAt, DateTime now)
    {
        if (!ReliabilityRules.IsSuspended(driver, now)) return null;
        // A pause covers instant work only; planned trips stay open to them.
        return departAt - now <= DriverSelectionRules.InstantHorizon ? ErrorCode.DriverSuspended : null;
    }

    public async Task<BaseResponse<ReliabilityOutput>> Mine()
    {
        var userId = securityManager.RequireUserId();
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null) return new BaseResponse<ReliabilityOutput>(default, ErrorCode.NotFound);

        var settings = await Settings();
        var now = DateTime.UtcNow;
        var since = now.AddDays(-settings.ReliabilityWindowDays);
        var recent = await eventRepository
            .Where(e => e.UserId == userId && e.CreationDate >= since)
            .OrderByDescending(e => e.Id)
            .Take(20)
            .ToListAsync();

        return new BaseResponse<ReliabilityOutput>(new ReliabilityOutput
        {
            PointsInWindow = await PointsInWindow(userId, ActiveRole.Driver, now, settings),
            WindowDays = settings.ReliabilityWindowDays,
            WarnPoints = settings.ReliabilityWarnPoints,
            SuspendPoints = settings.ReliabilitySuspendPoints,
            SuspendedUntil = ReliabilityRules.IsSuspended(user, now) ? user.SuspendedUntil : null,
            TripsAsDriver = user.TripsAsDriver,
            DriverCancellations = user.DriverCancellations,
            CompletionRate = ReliabilityRules.CompletionRate(user.TripsAsDriver, user.DriverCancellations),
            RiderLateCancels = user.RiderLateCancels,
            RiderNoShows = user.RiderNoShows,
            Recent = recent.Select(e => new ReliabilityEventRow(e)).ToList(),
        });
    }

    public async Task<BaseResponse<PageOutput<ReliabilityEventRow>>> List(PageInput page, int? userId, bool? needsReview)
    {
        IQueryable<ReliabilityEvent> query = eventRepository.Query().Include(e => e.User);
        if (userId != null) query = query.Where(e => e.UserId == userId);
        if (needsReview == true) query = query.Where(e => e.NeedsReview && e.WaivedAt == null);
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(e => e.User != null
                && (e.User.FirstName.Contains(term) || e.User.LastName.Contains(term) || e.User.Phone.Contains(term)));
        }

        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(e => e.Id).Paginate(page).ToListAsync();
        return new BaseResponse<PageOutput<ReliabilityEventRow>>(new PageOutput<ReliabilityEventRow>
        {
            TotalRows = total,
            Data = rows.Select(e => new ReliabilityEventRow(e)).ToList(),
        });
    }

    public async Task<BaseResponse<ReliabilityEventRow>> Get(int id)
    {
        var entry = await eventRepository.Query().Include(e => e.User).FirstOrDefaultAsync(e => e.Id == id);
        return entry == null
            ? new BaseResponse<ReliabilityEventRow>(default, ErrorCode.NotFound)
            : new BaseResponse<ReliabilityEventRow>(new ReliabilityEventRow(entry));
    }

    public async Task<BaseResponse<ReliabilityEventRow>> Waive(int id, WaiveInput input)
    {
        var adminId = securityManager.RequireUserId();
        var entry = eventRepository.FirstOrDefault(e => e.Id == id, q => q.Include(e => e.User));
        if (entry == null) return new BaseResponse<ReliabilityEventRow>(default, ErrorCode.NotFound);
        if (entry.IsWaived) return new BaseResponse<ReliabilityEventRow>(new ReliabilityEventRow(entry));

        var now = DateTime.UtcNow;
        entry.WaivedAt = now;
        entry.WaivedBy = adminId;
        var note = input.Note ?? input.WaiveNote;
        entry.WaiveNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        entry.NeedsReview = false;
        eventRepository.Update(entry);

        var user = entry.User ?? await userRepository.GetByIdAsync(entry.UserId);
        if (user != null)
        {
            switch (entry.Kind)
            {
                case ReliabilityEventKind.Cancel or ReliabilityEventKind.LateCancel
                    or ReliabilityEventKind.SeriesEndShortNotice:
                    user.DriverCancellations = Math.Max(0, user.DriverCancellations - 1);
                    break;
                case ReliabilityEventKind.RiderLateCancel:
                    user.RiderLateCancels = Math.Max(0, user.RiderLateCancels - 1);
                    break;
                case ReliabilityEventKind.RiderNoShow:
                    user.RiderNoShows = Math.Max(0, user.RiderNoShows - 1);
                    break;
            }
            userRepository.Update(user);
        }
        await unitOfWork.SaveAsync();

        // A waiver can bring a paused driver back under the line.
        if (user != null && ReliabilityRules.IsSuspended(user, now))
        {
            var settings = await Settings();
            var points = await PointsInWindow(user.Id, ActiveRole.Driver, now, settings);
            if (points < settings.ReliabilitySuspendPoints)
            {
                user.SuspendedUntil = null;
                userRepository.Update(user);
                await unitOfWork.SaveAsync();
            }
        }

        await auditService.LogAsync(AuditActions.AdminReliabilityWaive, nameof(ReliabilityEvent), entry.Id);
        return new BaseResponse<ReliabilityEventRow>(new ReliabilityEventRow(entry));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<ReliabilityEventKind> Classify(Trip trip, int ridersAffected, DateTime now,
        AppConfigurationOutput settings, TripStatus? statusAtCancel = null)
    {
        // A day of a series is skipped, not cancelled: its own notice, and a
        // number of free skips in the window.
        if ((trip.SeriesCommitmentId != null || trip.ScheduleId != null) && trip.DriverId is { } driverId)
        {
            var since = now.AddDays(-settings.ReliabilityWindowDays);
            var used = await eventRepository.CountAsync(e => e.UserId == driverId
                && e.Kind == ReliabilityEventKind.SeriesSkip
                && e.CreationDate >= since);
            return SeriesRules.ClassifySkip(now, trip.DepartAt, statusAtCancel ?? trip.Status, ridersAffected,
                settings.SeriesSkipNoticeHours, used, settings.SeriesFreeSkipsPerWindow);
        }

        return ReliabilityRules.ClassifyDriverCancel(
            now,
            acceptedAt: trip.CreationDate,
            departAt: trip.DepartAt,
            status: statusAtCancel ?? trip.Status,
            ridersAffected: ridersAffected,
            graceMinutes: settings.FreeCancelGraceMinutes,
            lateLeadMinutes: settings.LateCancelLeadMinutes);
    }

    private async Task RecordRider(Booking booking, Trip trip, ReliabilityEventKind kind, DateTime now,
        Action<User> bump)
    {
        eventRepository.Create(new ReliabilityEvent
        {
            UserId = booking.RiderId,
            Role = ActiveRole.Rider,
            Kind = kind,
            Points = ReliabilityRules.PointsFor(kind),
            TripId = trip.Id,
            BookingId = booking.Id,
            MinutesBeforeDeparture = (int)Math.Round((trip.DepartAt - now).TotalMinutes),
            CreatedBy = booking.RiderId,
        });
        var rider = await userRepository.GetByIdAsync(booking.RiderId);
        if (rider != null)
        {
            bump(rider);
            userRepository.Update(rider);
        }
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.ReliabilityRecord, nameof(Booking), booking.Id);
    }

    private async Task<int> PointsInWindow(int userId, ActiveRole role, DateTime now, AppConfigurationOutput settings)
    {
        var since = now.AddDays(-settings.ReliabilityWindowDays);
        var entries = await eventRepository
            .Where(e => e.UserId == userId && e.Role == role && e.CreationDate >= since && e.WaivedAt == null)
            .Select(e => e.Points)
            .ToListAsync();
        return entries.Sum();
    }

    private async Task ApplyStanding(User driver, DateTime now, AppConfigurationOutput settings)
    {
        var points = await PointsInWindow(driver.Id, ActiveRole.Driver, now, settings);
        var (warn, suspend) = ReliabilityRules.Standing(points,
            settings.ReliabilityWarnPoints, settings.ReliabilitySuspendPoints);

        if (suspend && settings.SuspensionDays > 0 && !ReliabilityRules.IsSuspended(driver, now))
        {
            driver.SuspendedUntil = now.AddDays(settings.SuspensionDays);
            userRepository.Update(driver);
            await unitOfWork.SaveAsync();
            await auditService.LogAsync(AuditActions.ReliabilitySuspend, nameof(User), driver.Id);
            await notificationService.Notify(driver.Id, NotificationTemplate.ReliabilitySuspendedDriver,
                args: new { until = driver.SuspendedUntil.Value.ToString("yyyy-MM-dd") },
                data: new { suspendedUntil = driver.SuspendedUntil });
        }
        else if (warn && !suspend)
        {
            await notificationService.Notify(driver.Id, NotificationTemplate.ReliabilityWarningDriver,
                args: new { points, limit = settings.ReliabilitySuspendPoints, days = settings.SuspensionDays },
                data: new { points });
        }
    }

    private async Task<AppConfigurationOutput> Settings() =>
        (await appConfigurationService.Get()).Data ?? new AppConfigurationOutput();
}

/// <summary>
/// Puts riders back on the market when the driver who took their request walks
/// away. A matched request never reopens — its id already points at a trip —
/// so the riders get a new one, linked back, with the same journey and the
/// same conditions.
/// </summary>
public class DemandRecoveryService : IDemandRecoveryService
{
    /// <summary>Too close to departure to find anyone else; the riders are told plainly instead.</summary>
    public static readonly TimeSpan MinimumLeadToReopen = TimeSpan.FromMinutes(15);

    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IDemandAlertService demandAlertService;
    private readonly IRepository<RideRequest> requestRepository;
    private readonly IRepository<RideRequestParticipant> participantRepository;

    public DemandRecoveryService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        INotificationService notificationService,
        IDemandAlertService demandAlertService,
        IRepository<RideRequest> requestRepository,
        IRepository<RideRequestParticipant> participantRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.demandAlertService = demandAlertService;
        this.requestRepository = requestRepository;
        this.participantRepository = participantRepository;
    }

    public Task<bool> WouldReopen(Trip trip)
    {
        var source = requestRepository.FirstOrDefault(r => r.MatchedTripId == trip.Id);
        return Task.FromResult(source != null && trip.DepartAt - DateTime.UtcNow > MinimumLeadToReopen);
    }

    public async Task<List<int>> ReopenFor(Trip trip, IReadOnlyCollection<Booking> cancelled)
    {
        var now = DateTime.UtcNow;
        var source = requestRepository.FirstOrDefault(r => r.MatchedTripId == trip.Id);
        if (source == null || trip.DepartAt - now <= MinimumLeadToReopen) return [];

        // Everyone who lost a seat, not only the original pool: a rider who
        // booked the spare seat from search is just as stranded.
        var riders = new List<Booking>();
        var seats = 0;
        foreach (var booking in cancelled.OrderBy(b => b.Id))
        {
            if (riders.Any(r => r.RiderId == booking.RiderId)) continue;
            if (seats + booking.Seats > RiderTripRules.MaxSeats) break;
            riders.Add(booking);
            seats += booking.Seats;
        }
        if (riders.Count == 0) return [];

        var request = new RideRequest
        {
            OriginAddress = source.OriginAddress,
            Origin = source.Origin,
            DestinationAddress = source.DestinationAddress,
            Destination = source.Destination,
            Route = source.Route,
            DepartAt = trip.DepartAt,
            TimeWindowMinutes = source.TimeWindowMinutes,
            SeatsRequested = seats,
            RadiusMeters = source.RadiusMeters,
            GenderPolicy = source.GenderPolicy,
            DriverGenderPolicy = source.DriverGenderPolicy,
            MinAge = source.MinAge,
            MaxAge = source.MaxAge,
            Status = RideRequestStatus.Open,
            ReopenedFromRequestId = source.Id,
        };

        await unitOfWork.BeginTransactionAsync();
        try
        {
            requestRepository.Create(request);
            await unitOfWork.SaveAsync();
            foreach (var booking in riders)
            {
                participantRepository.Create(new RideRequestParticipant
                {
                    RideRequestId = request.Id,
                    RiderId = booking.RiderId,
                    Seats = booking.Seats,
                    Status = RideRequestParticipantStatus.Active,
                    CoRiderGenderPolicy = booking.CoRiderGenderPolicy,
                    MinAge = booking.MinAge,
                    MaxAge = booking.MaxAge,
                    SharedTermsAcceptedAt = booking.SharedTermsAcceptedAt,
                });
            }
            await unitOfWork.CommitAsync();
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }

        await auditService.LogAsync(AuditActions.RideRequestReopen, nameof(RideRequest), request.Id);

        if (RiderTripRules.ShouldNotifyNow(request.DepartAt, now))
        {
            request.NotifiedAt = now;
            requestRepository.Update(request);
            await unitOfWork.SaveAsync();
            await notificationService.NotifyNearbyDrivers(request);
        }
        await demandAlertService.Match(request);

        var riderIds = riders.Select(b => b.RiderId).Distinct().ToList();
        await notificationService.NotifyMany(riderIds, NotificationTemplate.TripCancelledReopenedRider,
            args: new { origin = request.OriginAddress, destination = request.DestinationAddress },
            data: new { tripId = trip.Id, rideRequestId = request.Id });
        return riderIds;
    }
}
