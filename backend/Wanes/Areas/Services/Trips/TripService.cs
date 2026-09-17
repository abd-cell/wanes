using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.Series;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Marketplace;
using Wanes.Areas.Services.Marketplace.Models;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Trips.Models;
using Wanes.Areas.Services.Users.Availability;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Trips;

public class TripService : ITripService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IDriverAvailabilityService driverAvailabilityService;
    private readonly IAppConfigurationService appConfigurationService;
    private readonly IReliabilityService reliabilityService;
    private readonly IDemandRecoveryService demandRecoveryService;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<TripStatusHistory> tripHistoryRepository;
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<Booking> bookingRepository;


    public TripService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IDriverAvailabilityService driverAvailabilityService,
        IAppConfigurationService appConfigurationService,
        IReliabilityService reliabilityService,
        IDemandRecoveryService demandRecoveryService,
        IRepository<Trip> tripRepository,
        IRepository<TripStatusHistory> tripHistoryRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<User> userRepository,
        IRepository<Booking> bookingRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.driverAvailabilityService = driverAvailabilityService;
        this.appConfigurationService = appConfigurationService;
        this.reliabilityService = reliabilityService;
        this.demandRecoveryService = demandRecoveryService;
        this.tripRepository = tripRepository;
        this.tripHistoryRepository = tripHistoryRepository;
        this.vehicleRepository = vehicleRepository;
        this.userRepository = userRepository;
        this.bookingRepository = bookingRepository;
    }

    /// <summary>
    /// The marketplace's seed for a trip whose driver named no threshold.
    /// Read per call rather than cached: an admin who raises it means the next
    /// trip, not the next restart.
    /// </summary>
    private async Task<bool> BoardingCodeRequired() =>
        (await appConfigurationService.Get()).Data?.BoardingCodeRequired ?? true;

    private async Task<int> MinimumPassengersDefault() =>
        (await appConfigurationService.Get()).Data?.MinimumPassengersDefault
        ?? TripConfirmationRules.DefaultMinimumPassengers;

    public async Task<BaseResponse<TripOutput>> Create(CreateTripInput input)
    {
        var driverId = securityManager.RequireUserId();

        var driver = await userRepository.GetByIdAsync(driverId);
        if (driver == null) return new BaseResponse<TripOutput>(default, ErrorCode.NotFound);

        // No admin sign-off on trips: owning the vehicle is enough, and the trip
        // goes out as Posted the moment the driver creates it.
        var vehicle = vehicleRepository.FirstOrDefault(v => v.Id == input.VehicleId && v.UserId == driverId);
        if (vehicle == null) return new BaseResponse<TripOutput>(default, ErrorCode.VehicleNotFound);

        if (input.DepartAt <= DateTime.UtcNow)
            return new BaseResponse<TripOutput>(default, ErrorCode.DepartureMustBeFuture);
        if (IsSamePoint(input.Origin, input.Destination))
            return new BaseResponse<TripOutput>(default, ErrorCode.OriginEqualsDestination);

        // One driver, one car: they cannot post a trip while out on one, nor two
        // trips leaving at about the same time. A posted trip is a promise to
        // riders, so this is guarded when it is made rather than when it breaks.
        if (await driverAvailabilityService.CheckCanCommit(driverId, input.DepartAt) is { } busy)
            return new BaseResponse<TripOutput>(default, busy);

        // seats offered defaults to capacity, and must never exceed it
        var seats = input.SeatsTotal <= 0 ? vehicle.SeatCapacity : input.SeatsTotal;
        if (seats > vehicle.SeatCapacity)
            return new BaseResponse<TripOutput>(default, ErrorCode.SeatsExceedCapacity);

        // A threshold the trip cannot reach would cancel itself at the cutoff
        // however many riders turned up. Refused rather than clamped: the driver
        // meant something by the number, and quietly changing it would confirm a
        // trip they intended to be conditional.
        if (input.MinSeatsToConfirm > seats)
            return new BaseResponse<TripOutput>(default, ErrorCode.MinSeatsExceedTotal);

        var origin = GeoFactory.Point(input.Origin.Lat, input.Origin.Lng);
        var destination = GeoFactory.Point(input.Destination.Lat, input.Destination.Lng);

        var trip = new Trip
        {
            DriverId = driverId,
            VehicleId = vehicle.Id,
            OriginAddress = input.Origin.Address,
            Origin = origin,
            DestinationAddress = input.Destination.Address,
            Destination = destination,
            Route = GeoFactory.Line(origin, destination),   // straight line in the MVP
            DepartAt = input.DepartAt,
            SeatsTotal = seats,
            SeatsLeft = seats,
            PricePerSeat = input.PricePerSeat,
            MinSeatsToConfirm = TripConfirmationRules.SeededThresholdFor(
                input.MinSeatsToConfirm, seats, await MinimumPassengersDefault()),
            GenderPolicy = input.GenderPolicy,
            MinAge = input.MinAge,
            MaxAge = input.MaxAge,
            Status = TripStatus.Posted,
        };

        tripRepository.Create(trip);
        await unitOfWork.SaveAsync();
        AddHistory(trip.Id, TripStatus.Posted, driverId);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.TripCreate, nameof(Trip), trip.Id);

        // The other half of matching: riders sitting on their own open postings
        // along this route. This trip may be exactly what they asked for, and
        // without this they would never hear about it.
        await notificationService.NotifyWaitingRiders(trip, driver);

        return new BaseResponse<TripOutput>(new TripOutput(trip, driver, vehicle));
    }

    public async Task<BaseResponse<TripOutput>> Update(int id, UpdateTripInput input)
    {
        var driverId = securityManager.RequireUserId();

        var trip = tripRepository.FirstOrDefault(t => t.Id == id && t.DriverId == driverId,
            query => query.Include(t => t.Driver).Include(t => t.Vehicle));
        if (trip == null) return new BaseResponse<TripOutput>(default, ErrorCode.TripNotFound);

        // Only a still-posted trip nobody has taken a seat on can be edited; once
        // it carries a booking the driver has to cancel instead.
        if (trip.Status != TripStatus.Posted)
            return new BaseResponse<TripOutput>(default, ErrorCode.TripNotEditable);

        var hasBookings = await bookingRepository
            .Where(b => b.TripId == trip.Id && b.Status != BookingStatus.Cancelled)
            .AnyAsync();
        if (hasBookings) return new BaseResponse<TripOutput>(default, ErrorCode.TripNotEditable);

        var vehicle = vehicleRepository.FirstOrDefault(v => v.Id == input.VehicleId && v.UserId == driverId);
        if (vehicle == null) return new BaseResponse<TripOutput>(default, ErrorCode.VehicleNotFound);

        if (input.DepartAt <= DateTime.UtcNow)
            return new BaseResponse<TripOutput>(default, ErrorCode.DepartureMustBeFuture);
        if (IsSamePoint(input.Origin, input.Destination))
            return new BaseResponse<TripOutput>(default, ErrorCode.OriginEqualsDestination);

        // Moving a departure can walk it into another of the driver's trips, so
        // the same clash check runs here — against everything but this trip.
        if (await driverAvailabilityService.CheckCanCommit(driverId, input.DepartAt, ignoreTripId: trip.Id)
            is { } busy)
            return new BaseResponse<TripOutput>(default, busy);

        var seats = input.SeatsTotal <= 0 ? vehicle.SeatCapacity : input.SeatsTotal;
        if (seats > vehicle.SeatCapacity)
            return new BaseResponse<TripOutput>(default, ErrorCode.SeatsExceedCapacity);
        if (input.MinSeatsToConfirm > seats)
            return new BaseResponse<TripOutput>(default, ErrorCode.MinSeatsExceedTotal);

        var origin = GeoFactory.Point(input.Origin.Lat, input.Origin.Lng);
        var destination = GeoFactory.Point(input.Destination.Lat, input.Destination.Lng);

        trip.VehicleId = vehicle.Id;
        trip.OriginAddress = input.Origin.Address;
        trip.Origin = origin;
        trip.DestinationAddress = input.Destination.Address;
        trip.Destination = destination;
        trip.Route = GeoFactory.Line(origin, destination);
        trip.DepartAt = input.DepartAt;
        trip.SeatsTotal = seats;
        trip.SeatsLeft = seats;              // no bookings yet, so every seat is free
        trip.PricePerSeat = input.PricePerSeat;
        trip.MinSeatsToConfirm = TripConfirmationRules.SeededThresholdFor(
            input.MinSeatsToConfirm, seats, await MinimumPassengersDefault());
        trip.GenderPolicy = input.GenderPolicy;
        trip.MinAge = input.MinAge;
        trip.MaxAge = input.MaxAge;

        // A trip nobody has booked has nothing to decide, so an edit clears any
        // prompt it was already sent — the driver may have just moved it to a
        // better hour, and asking them again about the old deadline would be
        // asking about a trip that no longer exists.
        trip.ConfirmPromptedAt = null;

        tripRepository.Update(trip);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.TripUpdate, nameof(Trip), trip.Id);

        return new BaseResponse<TripOutput>(new TripOutput(trip, trip.Driver, vehicle));
    }

    public Task<BaseResponse<TripOutput>> Get(int id)
    {
        // History comes along so TripOutput can report when the trip started.
        var trip = tripRepository.FirstOrDefault(t => t.Id == id,
            query => query.Include(t => t.Driver).Include(t => t.Vehicle).Include(t => t.History));
        if (trip == null) return Task.FromResult(new BaseResponse<TripOutput>(default, ErrorCode.TripNotFound));
        return Task.FromResult(new BaseResponse<TripOutput>(new TripOutput(trip, trip.Driver)));
    }

    public async Task<BaseResponse<List<TripOutput>>> GetUserTrips()
    {
        var driverId = securityManager.RequireUserId();
        var trips = await tripRepository
            .Where(t => t.DriverId == driverId, query => query.Include(t => t.Driver).Include(t => t.Vehicle))
            .OrderByDescending(t => t.DepartAt)
            .ToListAsync();

        var data = trips.Select(t => new TripOutput(t, t.Driver)).ToList();
        return new BaseResponse<List<TripOutput>>(data);
    }

    /// <summary>
    /// What cancelling would cost the driver right now — shown on the confirm
    /// sheet so the consequence is read before it is paid.
    /// </summary>
    public async Task<BaseResponse<CancelPreviewOutput>> CancelPreview(int id)
    {
        var driverId = securityManager.RequireUserId();
        var trip = tripRepository.FirstOrDefault(t => t.Id == id && t.DriverId == driverId);
        if (trip == null) return new BaseResponse<CancelPreviewOutput>(default, ErrorCode.TripNotFound);
        if (trip.Status is TripStatus.Completed or TripStatus.Cancelled)
            return new BaseResponse<CancelPreviewOutput>(default, ErrorCode.TripNotBookable);

        var riders = await RidersAffected(trip.Id);
        var requeued = riders > 0 && await demandRecoveryService.WouldReopen(trip);
        return new BaseResponse<CancelPreviewOutput>(
            await reliabilityService.PreviewDriverCancel(trip, riders, requeued));
    }

    public async Task<BaseResponse> Cancel(int id, CancelTripInput? input = null)
    {
        var driverId = securityManager.RequireUserId();
        var trip = tripRepository.FirstOrDefault(t => t.Id == id && t.DriverId == driverId);
        if (trip == null) return new BaseResponse(ErrorCode.TripNotFound);
        if (trip.Status is TripStatus.Completed or TripStatus.Cancelled)
            return new BaseResponse(ErrorCode.TripNotBookable);

        // Walking away from people who depend on the trip needs a reason — it is
        // what an admin reads before deciding whether the record should count.
        var ridersAffected = await RidersAffected(trip.Id);
        if (ReliabilityRules.ReasonRequired(ridersAffected) && input?.Reason == null)
            return new BaseResponse(ErrorCode.CancelReasonRequired);

        List<Booking> cancelled;
        var statusAtCancel = trip.Status;

        await unitOfWork.BeginTransactionAsync();
        try
        {
            trip.Status = TripStatus.Cancelled;
            tripRepository.Update(trip);

            // auto-cancel all confirmed/pending bookings
            cancelled = await bookingRepository
                .Where(b => b.TripId == trip.Id &&
                    (b.Status == BookingStatus.Pending ||
                     b.Status == BookingStatus.Confirmed ||
                     b.Status == BookingStatus.Arrived))
                .ToListAsync();
            foreach (var booking in cancelled)
            {
                booking.Status = BookingStatus.Cancelled;
                bookingRepository.Update(booking);
            }

            AddHistory(trip.Id, TripStatus.Cancelled, driverId);
            await unitOfWork.CommitAsync();
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }

        await auditService.LogAsync(AuditActions.TripCancel, nameof(Trip), trip.Id);

        // Classified on where the trip stood when the driver walked away — a
        // driver already on the road is cancelling late whatever the clock says.
        await reliabilityService.RecordDriverCancel(trip, driverId, ridersAffected, input?.Reason, input?.Note,
            statusAtCancel);

        // A trip that came from a request puts its riders back on the market —
        // they hear "finding you another driver" rather than a dead end.
        var requeued = await demandRecoveryService.ReopenFor(trip, cancelled);

        // Everyone else who had a seat loses their ride — this is the one that
        // most needs to reach a backgrounded phone.
        var others = cancelled.Select(b => b.RiderId).Where(r => !requeued.Contains(r)).Distinct().ToList();
        if (trip.SeriesCommitmentId != null || trip.ScheduleId != null)
        {
            // One day of a series: name the day, and say the rest stands.
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
        else
        {
            await notificationService.NotifyMany(others, NotificationTemplate.TripCancelledRider,
                args: new { origin = trip.OriginAddress, destination = trip.DestinationAddress },
                data:
                new { tripId = trip.Id });
        }

        return new BaseResponse();
    }

    /// <summary>Riders holding a live seat that has not boarded — the people a cancellation strands.</summary>
    private Task<int> RidersAffected(int tripId) =>
        bookingRepository.CountAsync(b => b.TripId == tripId &&
            (b.Status == BookingStatus.Pending ||
             b.Status == BookingStatus.Confirmed ||
             b.Status == BookingStatus.Arrived));

    /// <summary>
    /// Who is riding. Scoped to the caller's own trip — a driver may see the
    /// riders on a trip they are driving and no other, so the driver id is part
    /// of the lookup rather than a check bolted on after it.
    /// </summary>
    public async Task<BaseResponse<List<TripBookingRow>>> GetTripBookings(int tripId)
    {
        var driverId = securityManager.RequireUserId();
        var trip = tripRepository.FirstOrDefault(t => t.Id == tripId && t.DriverId == driverId);
        if (trip == null) return new BaseResponse<List<TripBookingRow>>(default, ErrorCode.TripNotFound);

        var bookings = await bookingRepository
            .Where(b => b.TripId == tripId, query => query.Include(b => b.Rider))
            .OrderBy(b => b.Id)
            .ToListAsync();

        var rows = bookings.Select(b => new TripBookingRow(b)).ToList();
        return new BaseResponse<List<TripBookingRow>>(rows);
    }

    /// <summary>
    /// The driver's last reported position, for the rider's tracking map.
    ///
    /// Visible to the driver themselves and to riders holding a live seat, and
    /// to nobody else — a position is at least as sensitive as the phone number
    /// guarded the same way on <see cref="Bookings.Models.BookingOutput"/>. A
    /// finished or cancelled seat stops seeing it.
    /// </summary>
    public async Task<BaseResponse<DriverLocationOutput>> GetDriverLocation(int tripId)
    {
        var userId = securityManager.RequireUserId();
        var trip = tripRepository.FirstOrDefault(t => t.Id == tripId,
            query => query.Include(t => t.Driver));
        if (trip == null) return new BaseResponse<DriverLocationOutput>(default, ErrorCode.TripNotFound);

        if (trip.DriverId != userId)
        {
            // The live-seat set of BookingStatusRules.IsLive, spelled out because
            // this has to translate to SQL. Keep the two in step.
            var riding = await bookingRepository.AnyAsync(b =>
                b.TripId == tripId && b.RiderId == userId &&
                (b.Status == BookingStatus.Pending ||
                 b.Status == BookingStatus.Confirmed ||
                 b.Status == BookingStatus.Arrived ||
                 b.Status == BookingStatus.InProgress));
            if (!riding) return new BaseResponse<DriverLocationOutput>(default, ErrorCode.Forbidden);
        }

        var point = trip.Driver?.LastLocation;
        // Success with no data: the driver simply has not reported yet, which is
        // an ordinary state on a trip that has not started, not a failure.
        if (point == null) return new BaseResponse<DriverLocationOutput>();

        return new BaseResponse<DriverLocationOutput>(new DriverLocationOutput
        {
            Lat = point.Y,
            Lng = point.X,
            ReportedAt = trip.Driver?.LastLocationAt,
            Online = trip.Driver?.IsOnline ?? false,
        });
    }

    public Task<BaseResponse<TripOutput>> Depart(int id) => Transition(id, TripStatus.EnRoute, AuditActions.TripDepart);
    public Task<BaseResponse<TripOutput>> Start(int id) => Transition(id, TripStatus.Active, AuditActions.TripStart);
    public Task<BaseResponse<TripOutput>> Arrive(int id) => Transition(id, TripStatus.Arrived, AuditActions.TripArrive);
    public Task<BaseResponse<TripOutput>> Complete(int id) => Transition(id, TripStatus.Completed, AuditActions.TripComplete);

    /// <summary>
    /// One rider's seat, moved by the driver carrying them. This is the primary
    /// way a trip advances: the trip-wide buttons are the same moves applied to
    /// everyone at once, and either way the trip's own status is read back off
    /// the seats through <see cref="TripStatusRules.Derive"/>.
    /// </summary>
    public async Task<BaseResponse<TripBookingRow>> SetBookingStatus(int tripId, int bookingId, BookingStatus status,
        string? boardingCode = null)
    {
        var driverId = securityManager.RequireUserId();

        // Scoped to the caller's own trip exactly as GetTripBookings is: a driver
        // tracks the riders they are carrying and nobody else's.
        var trip = tripRepository.FirstOrDefault(t => t.Id == tripId && t.DriverId == driverId,
            query => query.Include(t => t.Driver).Include(t => t.Vehicle));
        if (trip == null) return new BaseResponse<TripBookingRow>(default, ErrorCode.TripNotFound);
        if (trip.Status is TripStatus.Cancelled or TripStatus.Completed)
            return new BaseResponse<TripBookingRow>(default, ErrorCode.Conflict);

        // The whole manifest, because the trip's status is derived from all of it
        // and not just the seat being moved.
        var bookings = await bookingRepository
            .Where(b => b.TripId == tripId, query => query.Include(b => b.Rider))
            .ToListAsync();

        var booking = bookings.FirstOrDefault(b => b.Id == bookingId);
        if (booking == null) return new BaseResponse<TripBookingRow>(default, ErrorCode.BookingNotFound);
        if (!BookingStatusRules.CanDriverSet(booking.Status, status))
            return new BaseResponse<TripBookingRow>(default, ErrorCode.BookingStatusNotAllowed);

        // Boarding is the moment a stranger gets into the car: the rider reads
        // their code and the driver types it, so both know it is the right one.
        if (status == BookingStatus.InProgress
            && await BoardingCodeRequired()
            && !BoardingCodes.Matches(booking.BoardingCode, boardingCode))
            return new BaseResponse<TripBookingRow>(default, ErrorCode.BoardingCodeInvalid);

        booking.Status = status;
        bookingRepository.Update(booking);

        // A seat lost before departure goes back on the trip, exactly as a rider's
        // own cancel returns it. Once the trip has left, seats_left no longer
        // describes anything bookable, so it is left alone.
        if (status == BookingStatus.NoShow && TripStatusRules.IsOpenForSeats(trip.Status))
            trip.SeatsLeft += booking.Seats;

        ApplyDerivedStatus(trip, bookings, driverId);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditFor(status), nameof(Booking), booking.Id);

        if (status == BookingStatus.NoShow) await reliabilityService.RecordRiderNoShow(booking, trip);

        await NotifyRiders(trip, [booking.RiderId], status, booking.Id);

        return new BaseResponse<TripBookingRow>(new TripBookingRow(booking));
    }

    /// <summary>
    /// Which trip-wide moves a driver may make from where. Each one fans out to
    /// the riders it legally can, and the trip's status follows from the result —
    /// so this guards the driver's intent rather than the field itself.
    /// </summary>
    private static bool CanTransition(TripStatus from, TripStatus to) => to switch
    {
        // Setting off is only possible from a trip still waiting to go.
        TripStatus.EnRoute => TripStatusRules.IsOpenForSeats(from),

        // Reaching a kerb, or boarding someone, does not require having pressed
        // "set off" first — a driver who just starts collecting is not doing
        // anything wrong, and refusing them would only teach them to tap a
        // button that means nothing to them.
        TripStatus.Arrived => TripStatusRules.IsOpenForSeats(from) || from is TripStatus.EnRoute,
        TripStatus.Active => TripStatusRules.IsOpenForSeats(from)
            || from is TripStatus.EnRoute or TripStatus.Arrived,
        TripStatus.Completed => from is TripStatus.Active,
        _ => false,
    };

    /// <summary>
    /// A trip-wide move: the same per-seat move applied to every rider it is
    /// legal for — the driver saying "everybody in" instead of tapping each one.
    /// Seats it cannot reach (cancelled, a no-show, already there) are left alone.
    /// </summary>
    private async Task<BaseResponse<TripOutput>> Transition(int id, TripStatus to, string action)
    {
        var driverId = securityManager.RequireUserId();
        var trip = tripRepository.FirstOrDefault(t => t.Id == id && t.DriverId == driverId,
            query => query.Include(t => t.Driver).Include(t => t.Vehicle));
        if (trip == null) return new BaseResponse<TripOutput>(default, ErrorCode.TripNotFound);
        if (!CanTransition(trip.Status, to))
            return new BaseResponse<TripOutput>(default, ErrorCode.Conflict);

        var bookings = await bookingRepository
            .Where(b => b.TripId == trip.Id)
            .ToListAsync();

        var seatStatus = TripStatusRules.BookingStatusFor(to);
        List<Booking> moved = [];
        if (seatStatus is { } seat)
        {
            moved = bookings.Where(b => BookingStatusRules.CanDriverSet(b.Status, seat)).ToList();

            // "Everybody in" would board riders without their codes. With codes
            // on, each rider is boarded one at a time.
            if (seat == BookingStatus.InProgress
                && moved.Any(b => b.BoardingCode != null)
                && await BoardingCodeRequired())
                return new BaseResponse<TripOutput>(default, ErrorCode.BoardingCodeRequired);

            foreach (var booking in moved)
            {
                booking.Status = seat;
                bookingRepository.Update(booking);
            }
        }

        ApplyDerivedStatus(trip, bookings, driverId, intent: to);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(action, nameof(Trip), trip.Id);

        // Only the riders whose own seat moved. Telling a rider still waiting at
        // the curb that their trip has started, because somebody else boarded,
        // would be a lie the rail would then have to keep.
        await NotifyRiders(trip, moved.Select(b => b.RiderId).ToList(), seatStatus);

        return new BaseResponse<TripOutput>(new TripOutput(trip, trip.Driver));
    }

    /// <summary>
    /// Writes back the status the bookings imply, with its history row and the
    /// driver's trip count.
    ///
    /// <paramref name="intent"/> is the driver's own trip-wide move, and stands
    /// only where derivation has nothing to read: a trip whose riders all
    /// cancelled or no-showed still has a driver on the road who has to be able
    /// to finish it.
    /// </summary>
    private void ApplyDerivedStatus(Trip trip, List<Booking> bookings, int driverId, TripStatus? intent = null)
    {
        var before = trip.Status;
        var derived = TripStatusRules.Derive(before, bookings.Select(b => b.Status).ToList(), trip.SeatsLeft);
        trip.Status = derived == before && intent != null ? intent.Value : derived;
        tripRepository.Update(trip);

        if (trip.Status == before) return;

        AddHistory(trip.Id, trip.Status, driverId);

        // Setting out takes the driver off the hail board. The presence flag is
        // what the dashboard, the admin count and the push targeting all read,
        // so a driver on the road has to stop reading as "available" — they get
        // to go online again themselves once they are free.
        if (trip.Driver != null && DriverAvailabilityRules.IsEngaged(trip.Status) && trip.Driver.IsOnline)
        {
            trip.Driver.IsOnline = false;
            userRepository.Update(trip.Driver);
        }

        if (trip.Status == TripStatus.Completed && trip.Driver != null)
        {
            trip.Driver.TripsAsDriver++;
            userRepository.Update(trip.Driver);
        }
    }

    private static string AuditFor(BookingStatus status) => status switch
    {
        BookingStatus.Arrived => AuditActions.BookingArrive,
        BookingStatus.InProgress => AuditActions.BookingPickUp,
        BookingStatus.Completed => AuditActions.BookingDropOff,
        _ => AuditActions.BookingNoShow,
    };

    /// <summary>
    /// Tells the riders whose seat just moved, in the words that advance their
    /// tracking rail. One place, so the per-seat and trip-wide paths cannot
    /// notify differently about the same thing happening.
    /// </summary>
    private async Task NotifyRiders(Trip trip, IReadOnlyCollection<int> riderIds, BookingStatus? seatStatus,
        int? bookingId = null)
    {
        if (riderIds.Count == 0 || seatStatus == null) return;

        NotificationTemplate? template = seatStatus switch
        {
            BookingStatus.Arrived => NotificationTemplate.DriverArrivedRider,
            BookingStatus.InProgress => NotificationTemplate.TripStartedRider,
            BookingStatus.Completed => NotificationTemplate.TripCompletedRider,
            BookingStatus.NoShow => NotificationTemplate.BookingNoShowRider,
            _ => null,
        };
        if (template == null) return;

        object data = bookingId == null
            ? new { tripId = trip.Id }
            : new { tripId = trip.Id, bookingId };

        await notificationService.NotifyMany(riderIds, template.Value,
            args: new { origin = trip.OriginAddress, destination = trip.DestinationAddress },
            data: data);
    }

    private void AddHistory(int tripId, TripStatus status, int changedBy) =>
        tripHistoryRepository.Create(new TripStatusHistory
        {
            TripId = tripId,
            Status = status,
            ChangedBy = changedBy,
        });

    private static bool IsSamePoint(GeoPoint a, GeoPoint b) =>
        Math.Abs(a.Lat - b.Lat) < 1e-6 && Math.Abs(a.Lng - b.Lng) < 1e-6;
}
