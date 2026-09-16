using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Schedules;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Schedules.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Schedules;

public class TripScheduleService : ITripScheduleService
{
    /// <summary>How many upcoming departures a schedule's row previews.</summary>
    private const int PreviewCount = 5;

    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly IAppConfigurationService appConfigurationService;
    private readonly IRepository<TripSchedule> scheduleRepository;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<RideRequest> requestRepository;
    private readonly IRepository<RideRequestParticipant> participantRepository;
    private readonly IRepository<Booking> bookingRepository;
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IRepository<User> userRepository;

    public TripScheduleService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        IAppConfigurationService appConfigurationService,
        IRepository<TripSchedule> scheduleRepository,
        IRepository<Trip> tripRepository,
        IRepository<RideRequest> requestRepository,
        IRepository<RideRequestParticipant> participantRepository,
        IRepository<Booking> bookingRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.appConfigurationService = appConfigurationService;
        this.scheduleRepository = scheduleRepository;
        this.tripRepository = tripRepository;
        this.requestRepository = requestRepository;
        this.participantRepository = participantRepository;
        this.bookingRepository = bookingRepository;
        this.vehicleRepository = vehicleRepository;
        this.userRepository = userRepository;
    }

    public async Task<BaseResponse<TripScheduleRow>> Create(TripScheduleInput input)
    {
        var ownerId = securityManager.RequireUserId();

        if (Validate(input) is { } invalid)
            return new BaseResponse<TripScheduleRow>(default, invalid);
        if (await VehicleRefusal(input, ownerId) is { } noVehicle)
            return new BaseResponse<TripScheduleRow>(default, noVehicle);

        var schedule = new TripSchedule { OwnerId = ownerId };
        Apply(schedule, input);

        scheduleRepository.Create(schedule);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.ScheduleCreate, nameof(TripSchedule), schedule.Id);

        return new BaseResponse<TripScheduleRow>(Row(schedule));
    }

    public async Task<BaseResponse<TripScheduleRow>> Update(int id, TripScheduleInput input)
    {
        var ownerId = securityManager.RequireUserId();

        var schedule = scheduleRepository.FirstOrDefault(s => s.Id == id && s.OwnerId == ownerId);
        if (schedule == null) return new BaseResponse<TripScheduleRow>(default, ErrorCode.ScheduleNotFound);

        if (Validate(input) is { } invalid)
            return new BaseResponse<TripScheduleRow>(default, invalid);
        if (await VehicleRefusal(input, ownerId) is { } noVehicle)
            return new BaseResponse<TripScheduleRow>(default, noVehicle);

        var before = Row(schedule);
        Apply(schedule, input);
        scheduleRepository.Update(schedule);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.ScheduleUpdate, nameof(TripSchedule), schedule.Id,
            before, Row(schedule));

        return new BaseResponse<TripScheduleRow>(Row(schedule));
    }

    /// <summary>
    /// Deletes the schedule and cancels the occurrences it has already written
    /// that nobody is on.
    ///
    /// A booked occurrence is left running. It stopped being "part of a series"
    /// the moment somebody took a seat on it — cancelling it because its
    /// generator was deleted would call off a real ride to tidy up a
    /// configuration row.
    /// </summary>
    public async Task<BaseResponse> Delete(int id)
    {
        var ownerId = securityManager.RequireUserId();

        var schedule = scheduleRepository.FirstOrDefault(s => s.Id == id && s.OwnerId == ownerId);
        if (schedule == null) return new BaseResponse(ErrorCode.ScheduleNotFound);

        var now = DateTime.UtcNow;

        var futureTrips = await tripRepository
            .Where(t => t.ScheduleId == schedule.Id
                        && t.DepartAt > now
                        && t.Status == TripStatus.Posted)
            .ToListAsync();
        var bookedTripIds = await bookingRepository
            .Where(b => futureTrips.Select(t => t.Id).Contains(b.TripId)
                        && b.Status != BookingStatus.Cancelled)
            .Select(b => b.TripId)
            .ToListAsync();

        foreach (var trip in futureTrips.Where(t => !bookedTripIds.Contains(t.Id)))
        {
            trip.Status = TripStatus.Cancelled;
            tripRepository.Update(trip);
        }

        // A generated request is "booked" when somebody other than the owner
        // joined it — that pool is those riders' now, not the schedule's.
        var futureRequests = await requestRepository
            .Where(r => r.ScheduleId == schedule.Id
                        && r.DepartAt > now
                        && r.Status == RideRequestStatus.Open)
            .ToListAsync();

        if (futureRequests.Count != 0)
        {
            var requestIds = futureRequests.Select(r => r.Id).ToList();
            var joinedIds = await participantRepository
                .Where(p => requestIds.Contains(p.RideRequestId)
                            && p.Status == RideRequestParticipantStatus.Active
                            && p.RiderId != schedule.OwnerId)
                .Select(p => p.RideRequestId)
                .Distinct()
                .ToListAsync();

            foreach (var request in futureRequests.Where(r => !joinedIds.Contains(r.Id)))
            {
                request.Status = RideRequestStatus.Cancelled;
                requestRepository.Update(request);
            }
        }

        scheduleRepository.SoftDelete(schedule);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.ScheduleDelete, nameof(TripSchedule), schedule.Id);

        return new BaseResponse();
    }

    public async Task<BaseResponse<List<TripScheduleRow>>> GetUserSchedules()
    {
        var ownerId = securityManager.RequireUserId();
        var schedules = await scheduleRepository
            .Where(s => s.OwnerId == ownerId)
            .OrderByDescending(s => s.Id)
            .ToListAsync();

        return new BaseResponse<List<TripScheduleRow>>(schedules.Select(Row).ToList());
    }

    public Task<BaseResponse<TripScheduleRow>> Get(int id)
    {
        var ownerId = securityManager.RequireUserId();
        var schedule = scheduleRepository.FirstOrDefault(s => s.Id == id && s.OwnerId == ownerId);
        return Task.FromResult(schedule == null
            ? new BaseResponse<TripScheduleRow>(default, ErrorCode.ScheduleNotFound)
            : new BaseResponse<TripScheduleRow>(Row(schedule)));
    }

    /// <summary>
    /// One materialisation pass over every live schedule.
    ///
    /// Deliberately dull. All the recurrence thinking is in
    /// <see cref="RecurrenceRules"/>, and all the trip thinking is in the rows
    /// this writes — which are ordinary trips and ordinary postings, with no
    /// flag saying a schedule made them beyond the key that stops them being
    /// made twice.
    /// </summary>
    public async Task<int> MaterialiseDue()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizonEnd = today.AddDays(RecurrenceRules.HorizonDays);

        var schedules = await scheduleRepository
            .Where(s => !s.IsPaused
                        && s.StartDate <= horizonEnd
                        && (s.EndDate == null || s.EndDate >= today))
            .ToListAsync();
        if (schedules.Count == 0) return 0;

        var settings = (await appConfigurationService.Get()).Data;
        var written = 0;

        foreach (var schedule in schedules)
        {
            var from = Later(schedule.StartDate, today);
            if (schedule.MaterialisedThrough is { } through) from = Later(from, through.AddDays(1));

            var to = schedule.EndDate is { } end && end < horizonEnd ? end : horizonEnd;
            if (from > to) continue;

            var dates = RecurrenceRules.Occurrences(
                schedule.Recurrence, schedule.DaysOfWeek, schedule.DayOfMonth, from, to);

            foreach (var date in dates)
                written += await Materialise(schedule, date, settings);

            schedule.MaterialisedThrough = to;
            scheduleRepository.Update(schedule);
            await unitOfWork.SaveAsync();
        }

        return written;
    }

    /// <summary>
    /// One occurrence. Returns 1 when a row was written, 0 when it was skipped —
    /// already generated, already in the past, or a departure the driver is not
    /// free for.
    /// </summary>
    private async Task<int> Materialise(TripSchedule schedule, DateOnly date,
        Configuration.Models.AppConfigurationOutput? settings)
    {
        var zone = RecurrenceRules.ZoneFor(schedule.TimeZoneId);
        var departAt = RecurrenceRules.ToUtc(date, schedule.TimeOfDay, zone);

        // Today's occurrence may already have gone by the time the pass runs —
        // a restart at noon on a schedule that leaves at eight.
        if (departAt <= DateTime.UtcNow) return 0;

        // One question now, not two: both sides generate a Trip, so the
        // idempotency key is the same key.
        var exists = await tripRepository.AnyAsync(
            t => t.ScheduleId == schedule.Id && t.OccurrenceDate == date);
        if (exists) return 0;

        var written = schedule.OwnerRole == ActiveRole.Driver
            ? await MaterialiseTrip(schedule, date, departAt)
            : await MaterialisePosting(schedule, date, departAt, settings);
        if (!written) return 0;

        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.ScheduleMaterialise, nameof(TripSchedule), schedule.Id,
            after: new { occurrenceDate = date, departAt });

        return 1;
    }

    /// <summary>
    /// A driver's occurrence.
    ///
    /// The availability rules apply per occurrence and are checked here, not at
    /// the schedule: a schedule cannot pre-book a driver's calendar, and one
    /// that clashes with something they have already promised is skipped for
    /// that date only. The rest of the series is unaffected, which is the
    /// behaviour a driver expects after moving one trip.
    /// </summary>
    private async Task<bool> MaterialiseTrip(TripSchedule schedule, DateOnly date, DateTime departAt)
    {
        var vehicle = schedule.VehicleId is { } vehicleId
            ? vehicleRepository.FirstOrDefault(v => v.Id == vehicleId && v.UserId == schedule.OwnerId)
            : vehicleRepository.Where(v => v.UserId == schedule.OwnerId)
                .OrderByDescending(v => v.IsDefault)
                .FirstOrDefault();
        if (vehicle == null) return false;

        // The same 30-minute envelope posting a trip by hand is held to. Read
        // straight from the rows rather than through IDriverAvailabilityService:
        // that service answers about the *caller*, and this pass runs with
        // nobody signed in.
        var clash = await tripRepository
            .Where(t => t.DriverId == schedule.OwnerId
                        && t.Status != TripStatus.Cancelled
                        && t.Status != TripStatus.Completed)
            .Select(t => t.DepartAt)
            .ToListAsync();
        if (clash.Any(d => DriverAvailabilityRules.Clashes(d, departAt))) return false;

        var seats = Math.Clamp(schedule.Seats, 1, vehicle.SeatCapacity);

        tripRepository.Create(new Trip
        {
            DriverId = schedule.OwnerId,
            VehicleId = vehicle.Id,
            OriginAddress = schedule.OriginAddress,
            Origin = schedule.Origin,
            DestinationAddress = schedule.DestinationAddress,
            Destination = schedule.Destination,
            Route = GeoFactory.Line(schedule.Origin, schedule.Destination),
            DepartAt = departAt,
            SeatsTotal = seats,
            SeatsLeft = seats,
            PricePerSeat = schedule.PricePerSeat,
            MinSeatsToConfirm = TripConfirmationRules.ThresholdFor(schedule.MinSeatsToConfirm, seats),
            GenderPolicy = schedule.GenderPolicy,
            MinAge = schedule.MinAge,
            MaxAge = schedule.MaxAge,
            Status = TripStatus.Posted,
            ScheduleId = schedule.Id,
            OccurrenceDate = date,
        });

        return true;
    }

    /// <summary>
    /// A rider's occurrence — the same posting they would have written by hand,
    /// with the owner holding its seats.
    /// </summary>
    private async Task<bool> MaterialisePosting(TripSchedule schedule, DateOnly date, DateTime departAt,
        Configuration.Models.AppConfigurationOutput? settings)
    {
        var owner = await userRepository.GetByIdAsync(schedule.OwnerId);
        if (owner == null) return false;

        // The lead-time rule holds for a generated posting exactly as for a
        // typed one: a driver still has to gather these riders. Only today's
        // occurrence can ever fail it, and skipping that date is the honest
        // answer — the posting could not have been served.
        var km = GeoDistance.Km(schedule.Origin, schedule.Destination);
        var speed = settings?.AverageSpeedKmh ?? RiderTripRules.DefaultAverageSpeedKmh;
        var seats = Math.Clamp(schedule.Seats, 1, RiderTripRules.MaxSeats);
        if (departAt < RiderTripRules.EarliestDeparture(DateTime.UtcNow, km, seats, speed)) return false;

        // The same shape RideRequestService.Create writes: demand with the owner
        // as its first participant. Generated or typed, it is one kind of row —
        // which is the point of a schedule being a generator and nothing else
        // (§11.1): the marketplace never learns that recurrence exists.
        var request = new RideRequest
        {
            OriginAddress = schedule.OriginAddress,
            Origin = schedule.Origin,
            DestinationAddress = schedule.DestinationAddress,
            Destination = schedule.Destination,
            Route = GeoFactory.Line(schedule.Origin, schedule.Destination),
            DepartAt = departAt,
            TimeWindowMinutes = (int)MatchRules.TimeWindow.TotalMinutes,
            SeatsRequested = seats,
            RadiusMeters = MatchRules.NearRadiusMeters,
            DriverGenderPolicy = schedule.GenderPolicy,
            GenderPolicy = schedule.CoRiderGenderPolicy,
            MinAge = schedule.MinAge,
            MaxAge = schedule.MaxAge,
            Status = RideRequestStatus.Open,
            ScheduleId = schedule.Id,
            OccurrenceDate = date,
        };

        requestRepository.Create(request);
        await unitOfWork.SaveAsync();

        participantRepository.Create(new RideRequestParticipant
        {
            RideRequestId = request.Id,
            RiderId = schedule.OwnerId,
            Seats = seats,
            Status = RideRequestParticipantStatus.Active,
            CoRiderGenderPolicy = schedule.CoRiderGenderPolicy,
            MinAge = schedule.MinAge,
            MaxAge = schedule.MaxAge,
        });
        return true;
    }

    /// <summary>
    /// The shape checks. A recurrence that names no dates is the one worth
    /// refusing outright — it would sit in the list looking live and never
    /// produce a thing.
    /// </summary>
    private static ErrorCode? Validate(TripScheduleInput input)
    {
        if (IsSamePoint(input.Origin, input.Destination)) return ErrorCode.OriginEqualsDestination;
        if (input.EndDate != null && input.EndDate < input.StartDate)
            return ErrorCode.ScheduleHasNoOccurrences;
        if (input.Recurrence == Recurrence.Weekly && input.DaysOfWeek == WeekDays.None)
            return ErrorCode.ScheduleHasNoOccurrences;
        if (input.Recurrence == Recurrence.Monthly && input.DayOfMonth == null)
            return ErrorCode.ScheduleHasNoOccurrences;
        return null;
    }

    /// <summary>
    /// A driver's schedule needs a car, for the same reason a trip does: seats
    /// come from the vehicle, and a series that generates nothing because there
    /// was never a car to seat anybody in should fail when it is written, not
    /// silently every morning.
    /// </summary>
    private async Task<ErrorCode?> VehicleRefusal(TripScheduleInput input, int ownerId)
    {
        if (input.OwnerRole != ActiveRole.Driver) return null;

        var hasVehicle = input.VehicleId is { } id
            ? await vehicleRepository.AnyAsync(v => v.Id == id && v.UserId == ownerId)
            : await vehicleRepository.AnyAsync(v => v.UserId == ownerId);
        return hasVehicle ? null : ErrorCode.VehicleNotFound;
    }

    private static void Apply(TripSchedule schedule, TripScheduleInput input)
    {
        schedule.OwnerRole = input.OwnerRole;
        schedule.OriginAddress = input.Origin.Address;
        schedule.Origin = GeoFactory.Point(input.Origin.Lat, input.Origin.Lng);
        schedule.DestinationAddress = input.Destination.Address;
        schedule.Destination = GeoFactory.Point(input.Destination.Lat, input.Destination.Lng);
        schedule.Recurrence = input.Recurrence;
        schedule.DaysOfWeek = input.Recurrence == Recurrence.Weekly ? input.DaysOfWeek : WeekDays.None;
        schedule.DayOfMonth = input.Recurrence == Recurrence.Monthly ? input.DayOfMonth : null;
        schedule.TimeOfDay = input.TimeOfDay;
        schedule.TimeZoneId = string.IsNullOrWhiteSpace(input.TimeZoneId) ? null : input.TimeZoneId.Trim();
        schedule.StartDate = input.StartDate;
        schedule.EndDate = input.EndDate;
        schedule.Seats = Math.Clamp(input.Seats, 1, RiderTripRules.MaxSeats);
        schedule.IsPaused = input.IsPaused;

        var isDriver = input.OwnerRole == ActiveRole.Driver;
        schedule.PricePerSeat = isDriver && input.PricePerSeat is { } price
            ? FareRules.PriceFor(price)
            : null;
        schedule.VehicleId = isDriver ? input.VehicleId : null;
        schedule.MinSeatsToConfirm = isDriver
            ? TripConfirmationRules.ThresholdFor(input.MinSeatsToConfirm, schedule.Seats)
            : TripConfirmationRules.NoThreshold;

        schedule.GenderPolicy = input.GenderPolicy;
        schedule.CoRiderGenderPolicy = input.CoRiderGenderPolicy;
        schedule.MinAge = input.MinAge;
        schedule.MaxAge = input.MaxAge;
    }

    /// <summary>
    /// A schedule with the next few departures it will produce, worked out the
    /// same way the materialiser does — the only honest preview of a generator
    /// is to run it.
    /// </summary>
    private static TripScheduleRow Row(TripSchedule schedule)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = Later(schedule.StartDate, today);
        var to = schedule.EndDate is { } end && end < from.AddDays(RecurrenceRules.HorizonDays)
            ? end
            : from.AddDays(RecurrenceRules.HorizonDays);

        var zone = RecurrenceRules.ZoneFor(schedule.TimeZoneId);
        var next = from > to
            ? []
            : RecurrenceRules
                .Occurrences(schedule.Recurrence, schedule.DaysOfWeek, schedule.DayOfMonth, from, to)
                .Select(d => RecurrenceRules.ToUtc(d, schedule.TimeOfDay, zone))
                .Where(d => d > DateTime.UtcNow)
                .Take(PreviewCount)
                .ToList();

        return new TripScheduleRow(schedule, next);
    }

    private static DateOnly Later(DateOnly a, DateOnly b) => a > b ? a : b;

    private static bool IsSamePoint(GeoPoint a, GeoPoint b) =>
        Math.Abs(a.Lat - b.Lat) < 1e-6 && Math.Abs(a.Lng - b.Lng) < 1e-6;
}
