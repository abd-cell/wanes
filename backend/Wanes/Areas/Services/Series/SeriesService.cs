using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.Schedules;
using Wanes.Areas.Domain.Series;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Bookings;
using Wanes.Areas.Services.Bookings.Models;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Configuration.Models;
using Wanes.Areas.Services.Marketplace;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.RideRequests;
using Wanes.Areas.Services.Series.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Series;

/// <summary>
/// Whole-series commitments. The schedule stays a generator and every day
/// stays an ordinary trip with ordinary seats: this service decides which
/// driver takes a day and which riders get a seat on it, as the day is
/// written, and what giving up days costs.
/// </summary>
public class SeriesService : ISeriesService
{
    private const int RecentlyEndedDays = 30;

    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IAppConfigurationService appConfigurationService;
    private readonly IDriverInterestService driverInterestService;
    private readonly IBookingService bookingService;
    private readonly IReliabilityService reliabilityService;
    private readonly IDemandRecoveryService demandRecoveryService;
    private readonly IRepository<SeriesCommitment> seriesRepository;
    private readonly IRepository<TripSchedule> scheduleRepository;
    private readonly IRepository<RideRequest> requestRepository;
    private readonly IRepository<RideRequestParticipant> participantRepository;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<TripStatusHistory> tripHistoryRepository;
    private readonly IRepository<Booking> bookingRepository;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<Vehicle> vehicleRepository;

    public SeriesService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IAppConfigurationService appConfigurationService,
        IDriverInterestService driverInterestService,
        IBookingService bookingService,
        IReliabilityService reliabilityService,
        IDemandRecoveryService demandRecoveryService,
        IRepository<SeriesCommitment> seriesRepository,
        IRepository<TripSchedule> scheduleRepository,
        IRepository<RideRequest> requestRepository,
        IRepository<RideRequestParticipant> participantRepository,
        IRepository<Trip> tripRepository,
        IRepository<TripStatusHistory> tripHistoryRepository,
        IRepository<Booking> bookingRepository,
        IRepository<User> userRepository,
        IRepository<Vehicle> vehicleRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.appConfigurationService = appConfigurationService;
        this.driverInterestService = driverInterestService;
        this.bookingService = bookingService;
        this.reliabilityService = reliabilityService;
        this.demandRecoveryService = demandRecoveryService;
        this.seriesRepository = seriesRepository;
        this.scheduleRepository = scheduleRepository;
        this.requestRepository = requestRepository;
        this.participantRepository = participantRepository;
        this.tripRepository = tripRepository;
        this.tripHistoryRepository = tripHistoryRepository;
        this.bookingRepository = bookingRepository;
        this.userRepository = userRepository;
        this.vehicleRepository = vehicleRepository;
    }

    // ── Driver serves a rider's series ───────────────────────────────────────

    public async Task<BaseResponse<SeriesRow>> Propose(int rideRequestId, ProposeSeriesInput input)
    {
        var driverId = securityManager.RequireUserId();
        var settings = await Settings();
        if (!settings.SeriesCommitmentsEnabled) return Fail<SeriesRow>(ErrorCode.SeriesDisabled);
        if (settings.RequireSharedTermsAcceptance && input.AcceptSharedTrip != true)
            return Fail<SeriesRow>(ErrorCode.SharedTermsNotAccepted);

        var request = requestRepository.FirstOrDefault(r => r.Id == rideRequestId);
        if (request == null) return Fail<SeriesRow>(ErrorCode.RideRequestNotFound);
        if (request.ScheduleId is not { } scheduleId) return Fail<SeriesRow>(ErrorCode.NotRecurring);

        var schedule = scheduleRepository.FirstOrDefault(s => s.Id == scheduleId);
        if (schedule == null || schedule.OwnerRole != ActiveRole.Rider || !IsRunning(schedule))
            return Fail<SeriesRow>(ErrorCode.NotRecurring);
        if (schedule.OwnerId == driverId) return Fail<SeriesRow>(ErrorCode.CannotServeOwnRequest);

        var driver = await userRepository.GetByIdAsync(driverId);
        if (driver == null) return Fail<SeriesRow>(ErrorCode.NotFound);
        if (driver.DriverStatus != DriverStatus.Verified) return Fail<SeriesRow>(ErrorCode.DriverNotVerified);
        if (RiderEligibilityRules.CheckDriver(driver, schedule.GenderPolicy) is { } notForYou)
            return Fail<SeriesRow>(notForYou);

        var vehicle = input.VehicleId is { } chosen
            ? vehicleRepository.FirstOrDefault(v => v.Id == chosen && v.UserId == driverId)
            : vehicleRepository.Where(v => v.UserId == driverId).OrderByDescending(v => v.IsDefault).FirstOrDefault();
        if (vehicle == null) return Fail<SeriesRow>(ErrorCode.VehicleNotFound);
        if (schedule.Seats > vehicle.SeatCapacity) return Fail<SeriesRow>(ErrorCode.SeatsExceedCapacity);
        if (input.SeatsOffered is { } offered && (offered < schedule.Seats || offered > vehicle.SeatCapacity))
            return Fail<SeriesRow>(ErrorCode.InvalidSeatsOffered);

        var days = SeriesRules.NormaliseDays(input.DaysOfWeek, schedule);
        if (days == null) return Fail<SeriesRow>(ErrorCode.ScheduleHasNoOccurrences);
        var today = Today();
        if (input.Until is { } until && until < today) return Fail<SeriesRow>(ErrorCode.ScheduleHasNoOccurrences);

        var live = await seriesRepository
            .Where(c => c.ScheduleId == schedule.Id && c.Side == SeriesSide.DriverServes
                        && (c.Status == SeriesStatus.Proposed || c.Status == SeriesStatus.Active))
            .ToListAsync();
        if (live.Any(c => c.Status == SeriesStatus.Active)) return Fail<SeriesRow>(ErrorCode.SeriesAlreadyTaken);

        // Offering twice is the same offer, with the new terms.
        var now = DateTime.UtcNow;
        var proposal = live.FirstOrDefault(c => c.DriverId == driverId) ?? new SeriesCommitment
        {
            ScheduleId = schedule.Id,
            Side = SeriesSide.DriverServes,
            Status = SeriesStatus.Proposed,
            DriverId = driverId,
            RiderId = schedule.OwnerId,
            DecideAt = now.AddHours(settings.SeriesDecisionHours),
            CreatedBy = driverId,
        };
        proposal.VehicleId = vehicle.Id;
        proposal.PricePerSeat = input.PricePerSeat is { } price
            ? FareRules.PriceFor(price)
            : FareRules.PerSeat(GeoDistance.Km(schedule.Origin, schedule.Destination),
                settings.FareBaseAmount, settings.FarePerKm);
        proposal.Seats = input.SeatsOffered;
        proposal.DaysOfWeek = days.Value;
        proposal.Until = input.Until;
        proposal.Message = string.IsNullOrWhiteSpace(input.Message) ? null : input.Message.Trim();
        proposal.SharedTermsAcceptedAt = input.AcceptSharedTrip == true ? now : null;

        if (proposal.Id == 0) seriesRepository.Create(proposal);
        else seriesRepository.Update(proposal);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.SeriesPropose, nameof(SeriesCommitment), proposal.Id);

        proposal.Driver = driver;
        proposal.Vehicle = vehicle;

        // With no time to decide, the marketplace decides now.
        if (proposal.DecideAt <= now)
        {
            await Activate(proposal, schedule, settings);
            return new BaseResponse<SeriesRow>(await RowFor(proposal, schedule, driverId));
        }

        var (daysEn, daysAr) = SeriesRules.DaysLabel(schedule, proposal.DaysOfWeek);
        await notificationService.Notify(schedule.OwnerId, NotificationTemplate.SeriesOfferRider,
            args: new
            {
                name = driver.FirstName,
                days = daysEn,
                daysAr,
                origin = schedule.OriginAddress,
                destination = schedule.DestinationAddress,
                price = proposal.PricePerSeat,
            },
            data: new { seriesId = proposal.Id, scheduleId = schedule.Id });

        return new BaseResponse<SeriesRow>(await RowFor(proposal, schedule, driverId));
    }

    public async Task<BaseResponse> Withdraw(int id)
    {
        var driverId = securityManager.RequireUserId();
        var proposal = seriesRepository.FirstOrDefault(c => c.Id == id && c.DriverId == driverId
                                                            && c.Side == SeriesSide.DriverServes);
        if (proposal == null) return new BaseResponse(ErrorCode.SeriesNotFound);
        if (proposal.Status == SeriesStatus.Withdrawn) return new BaseResponse();
        if (proposal.Status != SeriesStatus.Proposed) return new BaseResponse(ErrorCode.SeriesNotAllowed);

        proposal.Status = SeriesStatus.Withdrawn;
        proposal.EndedAt = DateTime.UtcNow;
        proposal.EndedBy = driverId;
        seriesRepository.Update(proposal);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.SeriesWithdraw, nameof(SeriesCommitment), proposal.Id);
        return new BaseResponse();
    }

    public async Task<BaseResponse<List<SeriesRow>>> OffersFor(int scheduleId)
    {
        var callerId = securityManager.RequireUserId();
        var schedule = scheduleRepository.FirstOrDefault(s => s.Id == scheduleId && s.OwnerId == callerId);
        if (schedule == null) return Fail<List<SeriesRow>>(ErrorCode.ScheduleNotFound);

        var rows = await seriesRepository
            .Where(c => c.ScheduleId == scheduleId
                        && (c.Status == SeriesStatus.Proposed || c.Status == SeriesStatus.Active),
                q => q.Include(c => c.Driver).Include(c => c.Rider).Include(c => c.Vehicle))
            .OrderByDescending(c => c.Status)
            .ThenBy(c => c.PricePerSeat)
            .ThenBy(c => c.Id)
            .ToListAsync();

        var result = new List<SeriesRow>();
        foreach (var c in rows) result.Add(await RowFor(c, schedule, callerId));
        return new BaseResponse<List<SeriesRow>>(result);
    }

    public async Task<BaseResponse<SeriesResult>> Accept(int id)
    {
        var callerId = securityManager.RequireUserId();
        var settings = await Settings();
        var proposal = seriesRepository.FirstOrDefault(c => c.Id == id && c.RiderId == callerId
                                                            && c.Side == SeriesSide.DriverServes,
            q => q.Include(c => c.Driver).Include(c => c.Vehicle));
        if (proposal == null) return Fail<SeriesResult>(ErrorCode.SeriesNotFound);

        var schedule = scheduleRepository.FirstOrDefault(s => s.Id == proposal.ScheduleId);
        if (schedule == null) return Fail<SeriesResult>(ErrorCode.NotRecurring);

        // Accepting what is already accepted is a success.
        if (proposal.Status == SeriesStatus.Active)
            return new BaseResponse<SeriesResult>(new SeriesResult { Series = await RowFor(proposal, schedule, callerId) });
        if (proposal.Status != SeriesStatus.Proposed) return Fail<SeriesResult>(ErrorCode.SeriesNotAllowed);
        if (!IsRunning(schedule)) return Fail<SeriesResult>(ErrorCode.NotRecurring);
        if (await seriesRepository.AnyAsync(c => c.ScheduleId == schedule.Id && c.Side == SeriesSide.DriverServes
                                                 && c.Status == SeriesStatus.Active))
            return Fail<SeriesResult>(ErrorCode.SeriesAlreadyTaken);

        var days = await Activate(proposal, schedule, settings);
        await auditService.LogAsync(AuditActions.SeriesAccept, nameof(SeriesCommitment), proposal.Id);
        return new BaseResponse<SeriesResult>(new SeriesResult
        {
            Series = await RowFor(proposal, schedule, callerId),
            Days = days,
        });
    }

    public async Task<BaseResponse> Decline(int id)
    {
        var callerId = securityManager.RequireUserId();
        var proposal = seriesRepository.FirstOrDefault(c => c.Id == id && c.RiderId == callerId
                                                            && c.Side == SeriesSide.DriverServes);
        if (proposal == null) return new BaseResponse(ErrorCode.SeriesNotFound);
        if (proposal.Status == SeriesStatus.Declined) return new BaseResponse();
        if (proposal.Status != SeriesStatus.Proposed) return new BaseResponse(ErrorCode.SeriesNotAllowed);

        proposal.Status = SeriesStatus.Declined;
        proposal.EndedAt = DateTime.UtcNow;
        proposal.EndedBy = callerId;
        seriesRepository.Update(proposal);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.SeriesDecline, nameof(SeriesCommitment), proposal.Id);

        var schedule = scheduleRepository.FirstOrDefault(s => s.Id == proposal.ScheduleId);
        await notificationService.Notify(proposal.DriverId, NotificationTemplate.SeriesNotSelectedDriver,
            args: new { origin = schedule?.OriginAddress, destination = schedule?.DestinationAddress },
            data: new { seriesId = proposal.Id });
        return new BaseResponse();
    }

    /// <summary>
    /// The driver takes the series: every other offer is answered, and every
    /// upcoming day already written becomes the driver's trip.
    /// </summary>
    private async Task<List<SeriesDayResult>> Activate(SeriesCommitment proposal, TripSchedule schedule,
        AppConfigurationOutput settings)
    {
        var now = DateTime.UtcNow;
        proposal.Status = SeriesStatus.Active;
        proposal.AcceptedAt = now;
        proposal.DecideAt = null;
        seriesRepository.Update(proposal);

        var others = await seriesRepository
            .Where(c => c.ScheduleId == schedule.Id && c.Id != proposal.Id
                        && c.Side == SeriesSide.DriverServes && c.Status == SeriesStatus.Proposed)
            .ToListAsync();
        foreach (var other in others)
        {
            other.Status = SeriesStatus.Declined;
            other.EndedAt = now;
            seriesRepository.Update(other);
        }
        await unitOfWork.SaveAsync();

        var open = await requestRepository
            .Where(r => r.ScheduleId == schedule.Id
                        && r.Status == RideRequestStatus.Open
                        && r.DepartAt > now)
            .OrderBy(r => r.DepartAt)
            .ToListAsync();

        var days = new List<SeriesDayResult>();
        foreach (var request in open)
        {
            var date = request.OccurrenceDate ?? DateOnly.FromDateTime(request.DepartAt);
            if (!proposal.Covers(date)) continue;
            var outcome = await driverInterestService.FormForSeries(request.Id, proposal);
            days.Add(new SeriesDayResult
            {
                Date = date,
                DepartAt = request.DepartAt,
                TripId = outcome.TripId,
                Refusal = Name(outcome.Refusal),
            });
        }

        var taken = days.Count(d => d.TripId != null);
        var (daysEn, daysAr) = SeriesRules.DaysLabel(schedule, proposal.DaysOfWeek);
        var driverName = proposal.Driver?.FirstName
                         ?? (await userRepository.GetByIdAsync(proposal.DriverId))?.FirstName;

        await notificationService.Notify(proposal.DriverId, NotificationTemplate.SeriesAcceptedDriver,
            args: new
            {
                origin = schedule.OriginAddress,
                destination = schedule.DestinationAddress,
                days = daysEn,
                daysAr,
                count = taken,
            },
            data: new { seriesId = proposal.Id });
        await notificationService.Notify(schedule.OwnerId, NotificationTemplate.SeriesStartedRider,
            args: new { name = driverName, days = daysEn, daysAr, count = taken },
            data: new { seriesId = proposal.Id, scheduleId = schedule.Id });
        await notificationService.NotifyMany(others.Select(o => o.DriverId).Distinct().ToList(),
            NotificationTemplate.SeriesNotSelectedDriver,
            args: new { origin = schedule.OriginAddress, destination = schedule.DestinationAddress },
            data: new { scheduleId = schedule.Id });

        // Days the driver could not take are ordinary demand; the rider hears which.
        foreach (var missed in days.Where(d => d.TripId == null))
            await TellDayOpen(proposal, schedule, missed.Date);

        return days;
    }

    // ── Rider joins a driver's series ────────────────────────────────────────

    public async Task<BaseResponse<SeriesResult>> Join(int tripId, JoinSeriesInput input)
    {
        var riderId = securityManager.RequireUserId();
        var settings = await Settings();
        if (!settings.SeriesCommitmentsEnabled) return Fail<SeriesResult>(ErrorCode.SeriesDisabled);

        var trip = tripRepository.FirstOrDefault(t => t.Id == tripId);
        if (trip == null) return Fail<SeriesResult>(ErrorCode.TripNotFound);
        if (trip.ScheduleId is not { } scheduleId) return Fail<SeriesResult>(ErrorCode.NotRecurring);

        var schedule = scheduleRepository.FirstOrDefault(s => s.Id == scheduleId);
        if (schedule == null || schedule.OwnerRole != ActiveRole.Driver || !IsRunning(schedule))
            return Fail<SeriesResult>(ErrorCode.NotRecurring);
        if (schedule.OwnerId == riderId) return Fail<SeriesResult>(ErrorCode.CannotBookOwnTrip);

        var rider = await userRepository.GetByIdAsync(riderId);
        if (rider == null) return Fail<SeriesResult>(ErrorCode.NotFound);
        if (RiderEligibilityRules.CheckRider(rider, schedule.Conditions, trip.DepartAt) is { } refusal)
            return Fail<SeriesResult>(refusal);

        var days = SeriesRules.NormaliseDays(input.DaysOfWeek, schedule);
        if (days == null) return Fail<SeriesResult>(ErrorCode.ScheduleHasNoOccurrences);
        var today = Today();
        if (input.Until is { } until && until < today) return Fail<SeriesResult>(ErrorCode.ScheduleHasNoOccurrences);

        if (await seriesRepository.AnyAsync(c => c.ScheduleId == schedule.Id && c.RiderId == riderId
                                                 && c.Side == SeriesSide.RiderJoins
                                                 && c.Status == SeriesStatus.Active))
            return Fail<SeriesResult>(ErrorCode.SeriesAlreadyCommitted);

        var now = DateTime.UtcNow;
        var commitment = new SeriesCommitment
        {
            ScheduleId = schedule.Id,
            Side = SeriesSide.RiderJoins,
            Status = SeriesStatus.Active,
            DriverId = schedule.OwnerId,
            RiderId = riderId,
            PricePerSeat = schedule.PricePerSeat,
            Seats = Math.Clamp(input.Seats, 1, RiderTripRules.MaxSeats),
            DaysOfWeek = days.Value,
            Until = input.Until,
            AcceptedAt = now,
            SharedTermsAcceptedAt = input.AcceptSharedRide == true ? now : null,
            CreatedBy = riderId,
        };
        seriesRepository.Create(commitment);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.SeriesJoin, nameof(SeriesCommitment), commitment.Id);

        var upcoming = await tripRepository
            .Where(t => t.ScheduleId == schedule.Id && t.DepartAt > now
                        && (t.Status == TripStatus.Posted || t.Status == TripStatus.Full))
            .OrderBy(t => t.DepartAt)
            .ToListAsync();

        var results = new List<SeriesDayResult>();
        foreach (var day in upcoming)
        {
            var date = day.OccurrenceDate ?? DateOnly.FromDateTime(day.DepartAt);
            if (!commitment.Covers(date)) continue;
            results.Add(await BookDay(commitment, day, input.AcceptSharedRide));
        }

        var booked = results.Count(r => r.BookingId != null);
        await notificationService.Notify(schedule.OwnerId, NotificationTemplate.SeriesRiderJoinedDriver,
            args: new
            {
                name = rider.FirstName,
                seats = commitment.Seats,
                count = booked,
                origin = schedule.OriginAddress,
                destination = schedule.DestinationAddress,
            },
            data: new { seriesId = commitment.Id, scheduleId = schedule.Id });

        commitment.Rider = rider;
        return new BaseResponse<SeriesResult>(new SeriesResult
        {
            Series = await RowFor(commitment, schedule, riderId),
            Days = results,
        });
    }

    private async Task<SeriesDayResult> BookDay(SeriesCommitment commitment, Trip trip, bool? acceptShared)
    {
        var date = trip.OccurrenceDate ?? DateOnly.FromDateTime(trip.DepartAt);
        var res = await bookingService.CreateForSeries(commitment.RiderId, new CreateBookingInput
        {
            TripId = trip.Id,
            Seats = commitment.Seats ?? 1,
            AcceptSharedRide = acceptShared ?? commitment.SharedTermsAcceptedAt != null,
        }, commitment.Id);

        return new SeriesDayResult
        {
            Date = date,
            DepartAt = trip.DepartAt,
            TripId = trip.Id,
            BookingId = res.Success ? res.Data?.Id : null,
            Refusal = res.Success ? null : Name(res.ErrorCode),
        };
    }

    // ── Either side ──────────────────────────────────────────────────────────

    public async Task<BaseResponse<List<SeriesRow>>> Mine()
    {
        var callerId = securityManager.RequireUserId();
        var since = DateTime.UtcNow.AddDays(-RecentlyEndedDays);
        var rows = await seriesRepository
            .Where(c => (c.DriverId == callerId || c.RiderId == callerId)
                        && (c.Status == SeriesStatus.Proposed || c.Status == SeriesStatus.Active
                            || (c.EndedAt != null && c.EndedAt >= since)),
                q => q.Include(c => c.Driver).Include(c => c.Rider).Include(c => c.Vehicle))
            .OrderBy(c => c.Status)
            .ThenByDescending(c => c.Id)
            .ToListAsync();

        var scheduleIds = rows.Select(c => c.ScheduleId).Distinct().ToList();
        var schedules = await scheduleRepository.Query(includeDeleted: true)
            .Where(s => scheduleIds.Contains(s.Id))
            .ToListAsync();

        var result = new List<SeriesRow>();
        foreach (var c in rows)
            result.Add(await RowFor(c, schedules.FirstOrDefault(s => s.Id == c.ScheduleId), callerId));
        return new BaseResponse<List<SeriesRow>>(result);
    }

    public async Task<BaseResponse<SeriesRow>> Get(int id)
    {
        var callerId = securityManager.RequireUserId();
        var c = seriesRepository.FirstOrDefault(x => x.Id == id && (x.DriverId == callerId || x.RiderId == callerId),
            q => q.Include(x => x.Driver).Include(x => x.Rider).Include(x => x.Vehicle));
        if (c == null) return Fail<SeriesRow>(ErrorCode.SeriesNotFound);
        return new BaseResponse<SeriesRow>(await RowFor(c, await ScheduleOf(c), callerId));
    }

    public async Task<BaseResponse<SeriesEndPreview>> EndPreview(int id)
    {
        var callerId = securityManager.RequireUserId();
        var c = seriesRepository.FirstOrDefault(x => x.Id == id && (x.DriverId == callerId || x.RiderId == callerId));
        if (c == null) return Fail<SeriesEndPreview>(ErrorCode.SeriesNotFound);
        if (c.Status != SeriesStatus.Active || !MayEnd(c, callerId)) return Fail<SeriesEndPreview>(ErrorCode.SeriesNotAllowed);

        var settings = await Settings();
        return new BaseResponse<SeriesEndPreview>(await Preview(c, callerId, settings));
    }

    private async Task<SeriesEndPreview> Preview(SeriesCommitment c, int callerId, AppConfigurationOutput settings)
    {
        var now = DateTime.UtcNow;
        var today = Today();
        var noticeEnd = SeriesRules.NoticeEnd(today, settings.SeriesEndNoticeDays);
        var days = await UpcomingDays(c, now);

        var preview = new SeriesEndPreview
        {
            NoticeDays = settings.SeriesEndNoticeDays,
            NoticeEnd = noticeEnd,
            DaysKept = days.Count(d => d.Date <= noticeEnd),
            DaysDroppedWithNotice = days.Count(d => d.Date > noticeEnd),
            DaysDroppedNow = days.Count,
            SuspendPoints = settings.ReliabilitySuspendPoints,
            Free = c.CommitterId != callerId,
        };
        if (preview.Free) return preview;

        if (c.Side == SeriesSide.DriverServes)
        {
            preview.PointsNow = days.Count(d => d.Date <= noticeEnd && d.Riders > 0)
                                * SeriesRules.EndShortNoticePoints;
            var mine = await reliabilityService.Mine();
            var current = mine.Data?.PointsInWindow ?? 0;
            preview.PointsAfter = current + preview.PointsNow;
            preview.WouldSuspend = preview.PointsNow > 0 && preview.PointsAfter >= settings.ReliabilitySuspendPoints;
        }
        else
        {
            preview.LateCancelsNow = days.Count(d =>
                SeriesRules.IsShortNotice(now, d.DepartAt, settings.SeriesSkipNoticeHours));
        }
        return preview;
    }

    public async Task<BaseResponse<SeriesRow>> End(int id, EndSeriesInput input)
    {
        var callerId = securityManager.RequireUserId();
        var c = seriesRepository.FirstOrDefault(x => x.Id == id && (x.DriverId == callerId || x.RiderId == callerId),
            q => q.Include(x => x.Driver).Include(x => x.Rider).Include(x => x.Vehicle));
        if (c == null) return Fail<SeriesRow>(ErrorCode.SeriesNotFound);
        if (c.Status == SeriesStatus.Ended)
            return new BaseResponse<SeriesRow>(await RowFor(c, await ScheduleOf(c), callerId));
        if (c.Status != SeriesStatus.Active || !MayEnd(c, callerId)) return Fail<SeriesRow>(ErrorCode.SeriesNotAllowed);

        // A driver walking away from riders has to say why, as on one day.
        if (c.Side == SeriesSide.DriverServes && c.CommitterId == callerId && input.Immediately
            && input.Reason == null)
            return Fail<SeriesRow>(ErrorCode.CancelReasonRequired);

        var settings = await Settings();
        await EndCommitment(c, callerId, input.Immediately, charge: c.CommitterId == callerId,
            input.Reason, input.Note, settings);
        await auditService.LogAsync(AuditActions.SeriesEnd, nameof(SeriesCommitment), c.Id);
        return new BaseResponse<SeriesRow>(await RowFor(c, await ScheduleOf(c), callerId));
    }

    /// <summary>
    /// The driver may end a series they drive; the rider may end a series they
    /// ride or release the driver of their own. A driver cannot end a rider's
    /// seat on their schedule — deleting the schedule is how a driver stops.
    /// </summary>
    private static bool MayEnd(SeriesCommitment c, int callerId) =>
        c.Side == SeriesSide.DriverServes || c.RiderId == callerId;

    private async Task EndCommitment(SeriesCommitment c, int endedBy, bool immediately, bool charge,
        CancelReason? reason, string? note, AppConfigurationOutput settings)
    {
        var now = DateTime.UtcNow;
        var today = Today();
        var noticeEnd = SeriesRules.NoticeEnd(today, settings.SeriesEndNoticeDays);
        var schedule = await ScheduleOf(c);

        c.EndRequestedAt = now;
        c.EndedBy = endedBy;
        c.EndReason = reason;
        c.EndNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var lastDay = immediately ? today.AddDays(-1) : noticeEnd;
        c.Until = c.Until is { } until && until < lastDay ? until : lastDay;
        if (immediately)
        {
            c.Status = SeriesStatus.Ended;
            c.EndedAt = now;
        }
        seriesRepository.Update(c);
        await unitOfWork.SaveAsync();

        if (c.Side == SeriesSide.DriverServes)
        {
            var trips = await tripRepository
                .Where(t => t.SeriesCommitmentId == c.Id && t.DepartAt > now
                            && (t.Status == TripStatus.Posted || t.Status == TripStatus.Full))
                .OrderBy(t => t.DepartAt)
                .ToListAsync();
            foreach (var trip in trips)
            {
                var date = trip.OccurrenceDate ?? DateOnly.FromDateTime(trip.DepartAt);
                if (!immediately && date <= noticeEnd) continue;
                var riders = await CancelDay(trip, endedBy);
                if (charge && riders > 0 && SeriesRules.InsideEndNotice(date, today, settings.SeriesEndNoticeDays))
                    await reliabilityService.RecordSeriesEnd(c.DriverId, trip, riders, reason, note);
            }
        }
        else
        {
            foreach (var (seat, trip) in await LiveSeats(c.Id))
            {
                if (trip.DepartAt <= now) continue;
                var date = trip.OccurrenceDate ?? DateOnly.FromDateTime(trip.DepartAt);
                if (!immediately && date <= noticeEnd) continue;
                await ReleaseSeat(seat, trip);
                if (charge) await reliabilityService.RecordRiderCancel(seat, trip);
            }
        }

        // The other side hears who ended it and when it stops.
        var otherId = endedBy == c.DriverId ? c.RiderId : c.DriverId;
        var ender = await userRepository.GetByIdAsync(endedBy);
        var (dateEn, dateAr) = SeriesRules.DateLabel(c.Until ?? today);
        await notificationService.Notify(otherId, NotificationTemplate.SeriesEnded,
            args: new
            {
                name = ender?.FirstName,
                origin = schedule?.OriginAddress,
                destination = schedule?.DestinationAddress,
                date = dateEn,
                dateAr,
            },
            data: new { seriesId = c.Id });
    }

    /// <summary>Calls off one formed day and puts its riders back on the market.</summary>
    private async Task<int> CancelDay(Trip trip, int changedBy)
    {
        List<Booking> cancelled;
        await unitOfWork.BeginTransactionAsync();
        try
        {
            trip.Status = TripStatus.Cancelled;
            tripRepository.Update(trip);
            cancelled = await bookingRepository
                .Where(b => b.TripId == trip.Id
                            && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed
                                || b.Status == BookingStatus.Arrived))
                .ToListAsync();
            foreach (var booking in cancelled)
            {
                booking.Status = BookingStatus.Cancelled;
                bookingRepository.Update(booking);
            }
            tripHistoryRepository.Create(new TripStatusHistory
            {
                TripId = trip.Id,
                Status = TripStatus.Cancelled,
                ChangedBy = changedBy,
            });
            await unitOfWork.CommitAsync();
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }

        await auditService.LogAsync(AuditActions.TripCancel, nameof(Trip), trip.Id);
        var requeued = await demandRecoveryService.ReopenFor(trip, cancelled);
        var others = cancelled.Select(b => b.RiderId).Where(r => !requeued.Contains(r)).Distinct().ToList();
        if (others.Count > 0)
        {
            var (dateEn, dateAr) = SeriesRules.DateLabel(trip.OccurrenceDate ?? DateOnly.FromDateTime(trip.DepartAt));
            await notificationService.NotifyMany(others, NotificationTemplate.SeriesDaySkippedRider,
                args: new
                {
                    origin = trip.OriginAddress,
                    destination = trip.DestinationAddress,
                    date = dateEn,
                    dateAr,
                },
                data: new { tripId = trip.Id });
        }
        return cancelled.Select(b => b.RiderId).Distinct().Count();
    }

    /// <summary>Gives one series seat back, as the rider cancelling it would.</summary>
    private async Task ReleaseSeat(Booking seat, Trip trip)
    {
        await unitOfWork.BeginTransactionAsync();
        try
        {
            seat.Status = BookingStatus.Cancelled;
            bookingRepository.Update(seat);
            if (TripStatusRules.IsOpenForSeats(trip.Status)) trip.SeatsLeft += seat.Seats;
            var statuses = bookingRepository.Where(b => b.TripId == trip.Id)
                .Select(b => new { b.Id, b.Status })
                .ToList()
                .Select(b => b.Id == seat.Id ? seat.Status : b.Status)
                .ToList();
            trip.Status = TripStatusRules.Derive(trip.Status, statuses, trip.SeatsLeft);
            tripRepository.Update(trip);
            await unitOfWork.CommitAsync();
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }
        await auditService.LogAsync(AuditActions.BookingCancel, nameof(Booking), seat.Id);
    }

    // ── Hooks ────────────────────────────────────────────────────────────────

    public async Task OnTripGenerated(Trip trip, TripSchedule schedule)
    {
        var date = trip.OccurrenceDate ?? DateOnly.FromDateTime(trip.DepartAt);
        var riders = await seriesRepository
            .Where(c => c.ScheduleId == schedule.Id && c.Side == SeriesSide.RiderJoins
                        && c.Status == SeriesStatus.Active)
            .OrderBy(c => c.Id)
            .ToListAsync();

        foreach (var commitment in riders.Where(c => c.Covers(date)))
        {
            var day = await BookDay(commitment, trip, null);
            if (day.BookingId != null) continue;

            var (dateEn, dateAr) = SeriesRules.DateLabel(date);
            await notificationService.Notify(commitment.RiderId, NotificationTemplate.SeriesDayNotBookedRider,
                args: new
                {
                    origin = schedule.OriginAddress,
                    destination = schedule.DestinationAddress,
                    date = dateEn,
                    dateAr,
                },
                data: new { seriesId = commitment.Id, tripId = trip.Id });
        }
    }

    public async Task OnRequestGenerated(RideRequest request, TripSchedule schedule)
    {
        var date = request.OccurrenceDate ?? DateOnly.FromDateTime(request.DepartAt);
        var commitment = seriesRepository.FirstOrDefault(c =>
            c.ScheduleId == schedule.Id && c.Side == SeriesSide.DriverServes && c.Status == SeriesStatus.Active);
        if (commitment == null || !commitment.Covers(date)) return;

        var outcome = await driverInterestService.FormForSeries(request.Id, commitment);
        if (outcome.TripId != null) return;

        // The driver cannot have this day. It stays on the board for anyone,
        // and both sides are told which day and why.
        await TellDayOpen(commitment, schedule, date);
        var (dateEn, dateAr) = SeriesRules.DateLabel(date);
        await notificationService.Notify(commitment.DriverId, NotificationTemplate.SeriesDayMissedDriver,
            args: new
            {
                origin = schedule.OriginAddress,
                destination = schedule.DestinationAddress,
                date = dateEn,
                dateAr,
            },
            data: new { seriesId = commitment.Id, rideRequestId = request.Id });
    }

    public async Task OnScheduleDeleted(TripSchedule schedule)
    {
        var live = await seriesRepository
            .Where(c => c.ScheduleId == schedule.Id
                        && (c.Status == SeriesStatus.Proposed || c.Status == SeriesStatus.Active))
            .ToListAsync();
        if (live.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var c in live)
        {
            c.Status = c.Status == SeriesStatus.Proposed ? SeriesStatus.Declined : SeriesStatus.Ended;
            c.EndedAt = now;
            c.EndRequestedAt ??= now;
            c.EndedBy = schedule.OwnerId;
            seriesRepository.Update(c);
        }
        await unitOfWork.SaveAsync();

        var owner = await userRepository.GetByIdAsync(schedule.OwnerId);
        var (dateEn, dateAr) = SeriesRules.DateLabel(Today());
        await notificationService.NotifyMany(
            live.Select(c => c.CommitterId).Where(id => id != schedule.OwnerId).Distinct().ToList(),
            NotificationTemplate.SeriesEnded,
            args: new
            {
                name = owner?.FirstName,
                origin = schedule.OriginAddress,
                destination = schedule.DestinationAddress,
                date = dateEn,
                dateAr,
            },
            data: new { scheduleId = schedule.Id });
    }

    private async Task TellDayOpen(SeriesCommitment commitment, TripSchedule schedule, DateOnly date)
    {
        var (dateEn, dateAr) = SeriesRules.DateLabel(date);
        await notificationService.Notify(schedule.OwnerId, NotificationTemplate.SeriesDayOpenRider,
            args: new { date = dateEn, dateAr },
            data: new { seriesId = commitment.Id, scheduleId = schedule.Id });
    }

    // ── Clocks ───────────────────────────────────────────────────────────────

    public async Task<int> DecideDue()
    {
        var now = DateTime.UtcNow;
        var due = await seriesRepository
            .Where(c => c.Side == SeriesSide.DriverServes && c.Status == SeriesStatus.Proposed
                        && c.DecideAt != null && c.DecideAt <= now)
            .ToListAsync();
        if (due.Count == 0) return 0;

        var settings = await Settings();
        var decided = 0;
        foreach (var group in due.GroupBy(c => c.ScheduleId))
        {
            var schedule = scheduleRepository.FirstOrDefault(s => s.Id == group.Key);
            var taken = await seriesRepository.AnyAsync(c => c.ScheduleId == group.Key
                && c.Side == SeriesSide.DriverServes && c.Status == SeriesStatus.Active);
            if (schedule == null || !IsRunning(schedule) || taken)
            {
                foreach (var stale in group)
                {
                    stale.Status = SeriesStatus.Declined;
                    stale.EndedAt = now;
                    seriesRepository.Update(stale);
                }
                await unitOfWork.SaveAsync();
                continue;
            }

            // Every live offer on the schedule competes, not only the due ones.
            var offers = await seriesRepository
                .Where(c => c.ScheduleId == group.Key && c.Side == SeriesSide.DriverServes
                            && c.Status == SeriesStatus.Proposed)
                .ToListAsync();
            var driverIds = offers.Select(o => o.DriverId).Distinct().ToList();
            var drivers = await userRepository.Where(u => driverIds.Contains(u.Id)).ToListAsync();
            var best = SeriesRules.Best(
                offers.Where(o => drivers.Any(d => d.Id == o.DriverId && d.DriverStatus == DriverStatus.Verified)),
                drivers.ToDictionary(d => d.Id));
            if (best == null) continue;

            best.Driver = drivers.First(d => d.Id == best.DriverId);
            await Activate(best, schedule, settings);
            await auditService.LogAsync(AuditActions.SeriesAccept, nameof(SeriesCommitment), best.Id);
            decided++;
        }
        return decided;
    }

    public async Task<int> CloseFinished()
    {
        var today = Today();
        var finished = await seriesRepository
            .Where(c => c.Status == SeriesStatus.Active && c.Until != null && c.Until < today)
            .ToListAsync();

        // A commitment on a schedule that has itself ended is finished too.
        var active = await seriesRepository
            .Where(c => c.Status == SeriesStatus.Active && (c.Until == null || c.Until >= today))
            .ToListAsync();
        if (active.Count > 0)
        {
            var ids = active.Select(c => c.ScheduleId).Distinct().ToList();
            var ended = await scheduleRepository
                .Where(s => ids.Contains(s.Id) && s.EndDate != null && s.EndDate < today)
                .Select(s => s.Id)
                .ToListAsync();
            finished.AddRange(active.Where(c => ended.Contains(c.ScheduleId)));
        }
        if (finished.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var c in finished)
        {
            c.Status = SeriesStatus.Ended;
            c.EndedAt = now;
            seriesRepository.Update(c);
        }
        await unitOfWork.SaveAsync();
        return finished.Count;
    }

    public async Task<int> SendWeeklySummaries()
    {
        var settings = await Settings();
        var now = DateTime.UtcNow;
        if ((int)now.DayOfWeek != settings.SeriesSummaryDay) return 0;

        var cutoff = now.AddDays(-6);
        var due = await seriesRepository
            .Where(c => c.Status == SeriesStatus.Active && (c.LastSummaryAt == null || c.LastSummaryAt < cutoff))
            .ToListAsync();

        var sent = 0;
        var weekEnd = now.AddDays(7);
        foreach (var c in due)
        {
            var schedule = await ScheduleOf(c);
            if (schedule == null) continue;

            var days = (await UpcomingDays(c, now)).Where(d => d.DepartAt <= weekEnd).ToList();
            c.LastSummaryAt = now;
            seriesRepository.Update(c);
            if (days.Count == 0) continue;

            // Both people on a driver's series hear; a rider's seat is the rider's news.
            var to = c.Side == SeriesSide.DriverServes ? new[] { c.DriverId, c.RiderId } : [c.RiderId];
            await notificationService.NotifyMany(to, NotificationTemplate.SeriesWeeklySummary,
                args: new
                {
                    count = days.Count,
                    origin = schedule.OriginAddress,
                    destination = schedule.DestinationAddress,
                },
                data: new { seriesId = c.Id });
            sent++;
        }
        await unitOfWork.SaveAsync();
        return sent;
    }

    // ── Admin ────────────────────────────────────────────────────────────────

    public async Task<BaseResponse<PageOutput<SeriesRow>>> List(PageInput page, SeriesStatus? status, SeriesSide? side)
    {
        IQueryable<SeriesCommitment> query = seriesRepository.Query()
            .Include(c => c.Driver).Include(c => c.Rider).Include(c => c.Vehicle).Include(c => c.Schedule);
        if (status != null) query = query.Where(c => c.Status == status);
        if (side != null) query = query.Where(c => c.Side == side);
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(c =>
                (c.Driver != null && (c.Driver.FirstName.Contains(term) || c.Driver.Phone.Contains(term)))
                || (c.Rider != null && (c.Rider.FirstName.Contains(term) || c.Rider.Phone.Contains(term)))
                || (c.Schedule != null && (c.Schedule.OriginAddress.Contains(term)
                                           || c.Schedule.DestinationAddress.Contains(term))));
        }

        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(c => c.Id).Paginate(page).ToListAsync();
        var now = DateTime.UtcNow;
        var data = new List<SeriesRow>();
        foreach (var c in rows)
        {
            var row = new SeriesRow(c, c.Schedule, 0);
            var days = await UpcomingDays(c, now);
            row.UpcomingCount = days.Count;
            row.NextDeparture = days.FirstOrDefault()?.DepartAt;
            data.Add(row);
        }
        return new BaseResponse<PageOutput<SeriesRow>>(new PageOutput<SeriesRow> { TotalRows = total, Data = data });
    }

    public async Task<BaseResponse<SeriesRow>> AdminEnd(int id)
    {
        var adminId = securityManager.RequireUserId();
        var c = seriesRepository.FirstOrDefault(x => x.Id == id,
            q => q.Include(x => x.Driver).Include(x => x.Rider).Include(x => x.Vehicle));
        if (c == null) return Fail<SeriesRow>(ErrorCode.SeriesNotFound);

        var settings = await Settings();
        if (c.Status == SeriesStatus.Proposed)
        {
            c.Status = SeriesStatus.Declined;
            c.EndedAt = DateTime.UtcNow;
            c.EndedBy = adminId;
            seriesRepository.Update(c);
            await unitOfWork.SaveAsync();
        }
        else if (c.Status == SeriesStatus.Active)
        {
            await EndCommitment(c, adminId, immediately: true, charge: false, null, "Ended by admin", settings);
        }
        await auditService.LogAsync(AuditActions.AdminSeriesEnd, nameof(SeriesCommitment), c.Id);
        return new BaseResponse<SeriesRow>(await RowFor(c, await ScheduleOf(c), 0));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed record Day(DateOnly Date, DateTime DepartAt, int Riders);

    /// <summary>The upcoming days already written under a commitment.</summary>
    private async Task<List<Day>> UpcomingDays(SeriesCommitment c, DateTime now)
    {
        if (c.Side == SeriesSide.DriverServes)
        {
            var trips = await tripRepository
                .Where(t => t.SeriesCommitmentId == c.Id && t.DepartAt > now
                            && (t.Status == TripStatus.Posted || t.Status == TripStatus.Full))
                .Select(t => new { t.Id, t.DepartAt, t.OccurrenceDate })
                .ToListAsync();
            var ids = trips.Select(t => t.Id).ToList();
            var seated = await bookingRepository
                .Where(b => ids.Contains(b.TripId)
                            && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                .Select(b => new { b.TripId, b.RiderId })
                .ToListAsync();
            return trips
                .Select(t => new Day(t.OccurrenceDate ?? DateOnly.FromDateTime(t.DepartAt), t.DepartAt,
                    seated.Where(s => s.TripId == t.Id).Select(s => s.RiderId).Distinct().Count()))
                .OrderBy(d => d.DepartAt)
                .ToList();
        }

        return (await LiveSeats(c.Id))
            .Where(s => s.Trip.DepartAt > now)
            .Select(s => new Day(s.Trip.OccurrenceDate ?? DateOnly.FromDateTime(s.Trip.DepartAt), s.Trip.DepartAt, 1))
            .OrderBy(d => d.DepartAt)
            .ToList();
    }

    /// <summary>A rider's live seats under a commitment, each with its trip.</summary>
    private async Task<List<(Booking Seat, Trip Trip)>> LiveSeats(int commitmentId)
    {
        var seats = await bookingRepository
            .Where(b => b.SeriesCommitmentId == commitmentId
                        && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
            .ToListAsync();
        var tripIds = seats.Select(b => b.TripId).Distinct().ToList();
        var trips = await tripRepository.Where(t => tripIds.Contains(t.Id)).ToListAsync();
        return seats
            .Select(b => (Seat: b, Trip: trips.FirstOrDefault(t => t.Id == b.TripId)))
            .Where(x => x.Trip != null)
            .Select(x => (x.Seat, x.Trip!))
            .ToList();
    }

    private async Task<SeriesRow> RowFor(SeriesCommitment c, TripSchedule? schedule, int callerId)
    {
        c.Driver ??= await userRepository.GetByIdAsync(c.DriverId);
        c.Rider ??= await userRepository.GetByIdAsync(c.RiderId);
        if (c.VehicleId is { } vehicleId) c.Vehicle ??= await vehicleRepository.GetByIdAsync(vehicleId);

        var row = new SeriesRow(c, schedule, callerId);
        if (c.Status == SeriesStatus.Active)
        {
            var days = await UpcomingDays(c, DateTime.UtcNow);
            row.UpcomingCount = days.Count;
            row.NextDeparture = days.FirstOrDefault()?.DepartAt;
        }
        return row;
    }

    private async Task<TripSchedule?> ScheduleOf(SeriesCommitment c) =>
        c.Schedule ?? await scheduleRepository.Query(includeDeleted: true)
            .FirstOrDefaultAsync(s => s.Id == c.ScheduleId);

    private static bool IsRunning(TripSchedule schedule) =>
        !schedule.IsDeleted && (schedule.EndDate == null || schedule.EndDate >= Today());

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static ErrorCodeName? Name(ErrorCode? code) =>
        code is { } c && c != ErrorCode.Success ? new ErrorCodeName((int)c, c.ToString()) : null;

    private async Task<AppConfigurationOutput> Settings() =>
        (await appConfigurationService.Get()).Data ?? new AppConfigurationOutput();

    private static BaseResponse<T> Fail<T>(ErrorCode code) => new(default, code);
}
