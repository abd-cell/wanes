using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Requests.Models;
using Wanes.Areas.Services.Users.Availability;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Requests;

public class RideRequestService : IRideRequestService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IDriverAvailabilityService driverAvailabilityService;
    private readonly IAppConfigurationService appConfigurationService;
    private readonly IRepository<RideRequest> rideRequestRepository;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<Booking> bookingRepository;

    public RideRequestService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IDriverAvailabilityService driverAvailabilityService,
        IAppConfigurationService appConfigurationService,
        IRepository<RideRequest> rideRequestRepository,
        IRepository<User> userRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<Trip> tripRepository,
        IRepository<Booking> bookingRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.driverAvailabilityService = driverAvailabilityService;
        this.appConfigurationService = appConfigurationService;
        this.rideRequestRepository = rideRequestRepository;
        this.userRepository = userRepository;
        this.vehicleRepository = vehicleRepository;
        this.tripRepository = tripRepository;
        this.bookingRepository = bookingRepository;
    }

    public async Task<BaseResponse<List<RideRequestRow>>> GetUserRequests()
    {
        var riderId = securityManager.RequireUserId();
        var requests = await rideRequestRepository
            .Where(r => r.RiderId == riderId)
            .OrderByDescending(r => r.Id).ToListAsync();

        var data = requests.Select(r => new RideRequestRow(r)).ToList();
        return new BaseResponse<List<RideRequestRow>>(data);
    }

    public async Task<BaseResponse> Cancel(int id)
    {
        var riderId = securityManager.RequireUserId();
        var request = rideRequestRepository.FirstOrDefault(r => r.Id == id && r.RiderId == riderId);
        if (request == null) return new BaseResponse(ErrorCode.RequestNotFound);
        if (request.Status != RideRequestStatus.Open) return new BaseResponse(ErrorCode.RequestNotOpen);

        request.Status = RideRequestStatus.Cancelled;
        rideRequestRepository.Update(request);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.RequestCancel, nameof(RideRequest), id);

        // The drivers this hail was pushed to are still holding its card. Filtering
        // it out of the next GetNearby is not enough — that only helps a driver who
        // happens to refresh, and the one staring at the sheet would still tap
        // Accept on a request the rider walked away from.
        await notificationService.NotifyRideRequestClosed(id, RideRequestStatus.Cancelled);
        return new BaseResponse();
    }

    public async Task<BaseResponse<List<RideRequestRow>>> GetNearby(double lat, double lng, int radiusMeters)
    {
        var driverId = securityManager.RequireUserId();

        // A driver out on the road can take nothing at all — not tonight's hail
        // either, by the same rule that stops them posting a trip while engaged.
        // Everything they see here, Accept must be able to honour.
        if (await driverAvailabilityService.IsEngaged(driverId))
            return new BaseResponse<List<RideRequestRow>>([]);

        // The scheduling clash, though, is per hail rather than per driver: each
        // one carries its own wanted departure, so a driver with a trip at nine
        // is refused the hail leaving at nine and still offered the one leaving
        // at six. Answering that with a single CheckCanCommit against "now" is
        // what used to blank the whole list for a driver with any trip on today.
        var committed = await driverAvailabilityService.CommittedDepartures(driverId);

        var origin = GeoFactory.Point(lat, lng);
        var radius = radiusMeters <= 0 ? 5000 : radiusMeters;

        // Two reaches, unioned: how far this driver is willing to look, and how
        // far the rider asked to be reached from. The second half is what makes
        // the list agree with the push — a rider who searched "Anywhere" gets a
        // 50 km hail, and the driver we already notified must be able to find it
        // here even though their own filter is narrower.
        var requests = await rideRequestRepository
            .Where(r => r.Status == RideRequestStatus.Open
                        && r.RiderId != driverId
                        && (r.ExpiresAt == null || r.ExpiresAt > DateTime.UtcNow)
                        && (r.Origin.IsWithinDistance(origin, radius)
                            || r.Origin.Distance(origin) <= r.RadiusMeters))
            .OrderBy(r => r.Origin.Distance(origin))
            .Take(30)
            .ToListAsync();

        // Drop the ones this driver could not accept anyway, so the list agrees
        // with what Accept will say and with the pushes their phone did or did
        // not get.
        var data = requests
            .Where(r => !committed.Any(d => DriverAvailabilityRules.Clashes(
                d, MatchRules.HailDepartureFor(r.WantedDepartAt, DateTime.UtcNow))))
            .Select(r => new RideRequestRow(r))
            .ToList();
        return new BaseResponse<List<RideRequestRow>>(data);
    }

    /// <summary>
    /// Takes a hail, at the price the driver is charging. First driver to accept
    /// wins, and the loser is told rather than allowed to create a second trip
    /// for the same rider.
    ///
    /// Two things make that true. The request carries a row version, so the
    /// Open → Matched write is a claim and not a read-then-write: the second
    /// driver's commit changes no rows, the whole transaction rolls back, and
    /// the trip and booking it had staged go with it. And the same driver
    /// arriving twice — a double tap, a retried request — is answered from the
    /// trip they already have rather than by building another.
    /// </summary>
    public async Task<BaseResponse<RideRequestRow>> Accept(int id, AcceptRideRequestInput? input = null)
    {
        var driverId = securityManager.RequireUserId();

        // Idempotency first, before anything is staged. A double tap must yield
        // one trip and say so twice; refusing the second tap with RequestNotOpen
        // would be a lie to the driver who is holding the trip.
        if (await AlreadyMine(id, driverId) is { } mine)
            return new BaseResponse<RideRequestRow>(mine);

        var driver = await userRepository.GetByIdAsync(driverId);
        if (driver == null) return new BaseResponse<RideRequestRow>(default, ErrorCode.NotFound);
        if (driver.DriverStatus != DriverStatus.Verified)
            return new BaseResponse<RideRequestRow>(default, ErrorCode.DriverNotVerified);

        // No admin sign-off on vehicles: owning one is enough to take a hail.
        var vehicle = vehicleRepository
            .Where(v => v.UserId == driverId)
            .OrderByDescending(v => v.IsDefault)
            .FirstOrDefault();
        if (vehicle == null) return new BaseResponse<RideRequestRow>(default, ErrorCode.VehicleNotFound);

        // The driver's own price is what the trip lists at. The configured rates
        // are only the fallback for a call that carried none — an older build,
        // or a retry whose body was lost — because the one thing this must not
        // produce is the single kind of trip in search with a blank price.
        //
        // Read before the transaction: a settings lookup is not part of the
        // claim, and Get() never fails.
        var settings = await appConfigurationService.Get();

        await unitOfWork.BeginTransactionAsync();
        try
        {
            var request = await rideRequestRepository.GetByIdAsync(id);
            if (request == null) return await RollBack(ErrorCode.RequestNotFound);
            if (request.Status != RideRequestStatus.Open) return await RollBack(ErrorCode.RequestNotOpen);

            // The sweeper flips Open → Expired on a timer, so between the TTL
            // running out and the next tick there is a window where the row still
            // reads Open. Honour the clock, not the column: a driver must not be
            // able to accept a hail the rider has already been told is over.
            if (request.ExpiresAt != null && request.ExpiresAt <= DateTime.UtcNow)
                return await RollBack(ErrorCode.RequestNotOpen);

            if (request.Seats > vehicle.SeatCapacity) return await RollBack(ErrorCode.SeatsExceedCapacity);

            // What the driver is actually committing to. A hail carries the
            // departure the rider searched for, so this is not always "now" —
            // and the availability question has to be asked about that moment,
            // not this one, or a driver free all evening would be refused
            // tonight's hail because of a trip they are running right now.
            //
            // Which is why the check sits inside the transaction, unlike the
            // vehicle and verification checks above: it needs the request row.
            var departAt = MatchRules.HailDepartureFor(request.WantedDepartAt, DateTime.UtcNow);
            if (await driverAvailabilityService.CheckCanCommit(driverId, departAt) is { } busy)
                return await RollBack(busy);

            // a driver accepting a hail creates a trip for it + a confirmed booking
            var trip = new Trip
            {
                DriverId = driverId,
                VehicleId = vehicle.Id,
                OriginAddress = request.OriginAddress,
                Origin = request.Origin,
                DestinationAddress = request.DestinationAddress,
                Destination = request.Destination,
                Route = GeoFactory.Line(request.Origin, request.Destination),
                // The rider's wanted departure, floored at the pickup lead: the
                // driver still has to reach the kerb, and a departure already in
                // the past is one search will never offer, which used to make
                // this trip unjoinable by anybody except the hailing rider.
                DepartAt = departAt,
                SeatsTotal = vehicle.SeatCapacity,
                SeatsLeft = vehicle.SeatCapacity - request.Seats,
                // Verbatim if the driver named a price, derived from the
                // distance if they somehow did not. Either way this trip carries
                // a figure: it is offered in search beside posted trips that all
                // show one, and a blank there reads as free rather than unset.
                PricePerSeat = input?.PricePerSeat is { } quoted
                    ? FareRules.PriceFor(quoted)
                    : FareRules.PerSeat(
                        GeoDistance.Km(request.Origin, request.Destination),
                        settings.Data?.FareBaseAmount ?? FareRules.DefaultBaseAmount,
                        settings.Data?.FarePerKm ?? FareRules.DefaultPerKm),
                Status = vehicle.SeatCapacity - request.Seats == 0 ? TripStatus.Full : TripStatus.Posted,
            };
            tripRepository.Create(trip);
            await unitOfWork.SaveAsync();

            bookingRepository.Create(new Booking
            {
                TripId = trip.Id,
                RiderId = request.RiderId,
                Seats = request.Seats,
                Status = BookingStatus.Confirmed,
                RideRequestId = request.Id,
            });

            // The claim. Conditional on the row version read above, so if another
            // driver matched this hail in the meantime the commit below throws
            // and everything staged in this transaction — trip included — is
            // discarded. That is what makes "first wins" true rather than hoped for.
            request.Status = RideRequestStatus.Matched;
            request.MatchedTripId = trip.Id;
            rideRequestRepository.Update(request);

            await unitOfWork.CommitAsync();
            await auditService.LogAsync(AuditActions.RequestAccept, nameof(RideRequest), request.Id);

            await notificationService.Notify(request.RiderId, NotificationTemplate.DriverAcceptedRider,
                args: new { name = driver.FirstName },
                data:
                new { requestId = request.Id, tripId = trip.Id });

            // Accepting is first-wins, so every other driver who was offered this
            // hail is now holding a card that can only fail. Close it on their
            // screens the same way a cancellation does.
            await notificationService.NotifyRideRequestClosed(request.Id, RideRequestStatus.Matched);

            // This trip is a real trip with real spare seats — the ones the
            // hailing rider did not take. Riders sitting on their own open hail
            // along the same route can have those seats, exactly as if the driver
            // had posted the trip themselves.
            if (trip.SeatsLeft > 0) await notificationService.NotifyWaitingRiders(trip, driver);

            return new BaseResponse<RideRequestRow>(new RideRequestRow(request));
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another driver got there first. Nothing of this attempt survives.
            await unitOfWork.RollBackAsync();
            unitOfWork.Detach();
            return new BaseResponse<RideRequestRow>(default, ErrorCode.RequestNotOpen);
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }
    }

    /// <summary>
    /// The row to hand back when this driver has already accepted this hail, or
    /// <c>null</c> when they have not.
    ///
    /// Deliberately narrow: only the driver who owns the matched trip is
    /// answered this way. Another driver asking about a hail that is already
    /// matched is a loser, not a repeat caller, and belongs on the refusal path.
    /// </summary>
    private async Task<RideRequestRow?> AlreadyMine(int id, int driverId)
    {
        var request = await rideRequestRepository.FirstOrDefaultAsync(r => r.Id == id);
        if (request?.Status != RideRequestStatus.Matched || request.MatchedTripId == null) return null;

        var mine = await tripRepository.AnyAsync(
            t => t.Id == request.MatchedTripId!.Value && t.DriverId == driverId);
        return mine ? new RideRequestRow(request) : null;
    }

    public async Task<int> ExpireDue()
    {
        var now = DateTime.UtcNow;
        var due = await rideRequestRepository
            .Where(r => r.Status == RideRequestStatus.Open
                        && r.ExpiresAt != null
                        && r.ExpiresAt <= now)
            .ToListAsync();
        if (due.Count == 0) return 0;

        foreach (var request in due)
        {
            request.Status = RideRequestStatus.Expired;
            rideRequestRepository.Update(request);
        }
        await unitOfWork.SaveAsync();

        foreach (var request in due)
        {
            // No actor on these: the sweeper runs outside any request, so the log
            // records what happened and leaves ActorUserId null.
            await auditService.LogAsync(AuditActions.RequestExpire, nameof(RideRequest), request.Id);
            await notificationService.NotifyRideRequestClosed(request.Id, RideRequestStatus.Expired);
        }

        return due.Count;
    }

    private async Task<BaseResponse<RideRequestRow>> RollBack(ErrorCode errorCode)
    {
        await unitOfWork.RollBackAsync();
        return new BaseResponse<RideRequestRow>(default, errorCode);
    }
}
