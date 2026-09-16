using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Configuration.Models;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.RideRequests.Models;
using Wanes.Areas.Services.Users.Availability;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.RideRequests;

/// <summary>
/// Demand, end to end on the riders' side: creating it, joining it, leaving it,
/// putting it in front of drivers, and letting it go when nothing came of it.
///
/// Everything here operates on <see cref="RideRequest"/> rows and their
/// <see cref="RideRequestParticipant"/>s. There is no <see cref="Trip"/> in this
/// file and no <c>Booking</c>: a booking is a seat on a ride, and until a driver
/// is selected there is no ride. That separation is the whole of v2's change —
/// see <see cref="RideRequest"/> for why it was worth the one thing it cost.
/// </summary>
public class RideRequestService : IRideRequestService
{
    /// <summary>How many requests one board page shows.</summary>
    private const int BoardSize = 30;

    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IDriverAvailabilityService driverAvailabilityService;
    private readonly IRiderAvailabilityService riderAvailabilityService;
    private readonly IAppConfigurationService appConfigurationService;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<RideRequest> requestRepository;
    private readonly IRepository<RideRequestParticipant> participantRepository;
    private readonly IRepository<DriverInterest> interestRepository;

    public RideRequestService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IDriverAvailabilityService driverAvailabilityService,
        IRiderAvailabilityService riderAvailabilityService,
        IAppConfigurationService appConfigurationService,
        IRepository<User> userRepository,
        IRepository<RideRequest> requestRepository,
        IRepository<RideRequestParticipant> participantRepository,
        IRepository<DriverInterest> interestRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.driverAvailabilityService = driverAvailabilityService;
        this.riderAvailabilityService = riderAvailabilityService;
        this.appConfigurationService = appConfigurationService;
        this.userRepository = userRepository;
        this.requestRepository = requestRepository;
        this.participantRepository = participantRepository;
        this.interestRepository = interestRepository;
    }

    /// <summary>
    /// Creates demand, with the author as its first participant.
    ///
    /// The one rule with teeth is the lead time: the departure has to leave a
    /// driver room to gather this many riders and run the leg
    /// (<see cref="RiderTripRules"/>). Everything else is shape — a route that
    /// goes somewhere, seats one car could carry, conditions in range.
    /// </summary>
    public async Task<BaseResponse<RideRequestRow>> Create(CreateRideRequestInput input)
    {
        var riderId = securityManager.RequireUserId();
        var rider = await userRepository.GetByIdAsync(riderId);
        if (rider == null) return new BaseResponse<RideRequestRow>(default, ErrorCode.NotFound);

        if (IsSamePoint(input.Origin, input.Destination))
            return new BaseResponse<RideRequestRow>(default, ErrorCode.OriginEqualsDestination);

        var seats = Math.Clamp(input.Seats, 1, RiderTripRules.MaxSeats);
        var settings = await Settings();

        var origin = GeoFactory.Point(input.Origin.Lat, input.Origin.Lng);
        var destination = GeoFactory.Point(input.Destination.Lat, input.Destination.Lng);
        var km = GeoDistance.Km(origin, destination);

        var now = DateTime.UtcNow;
        var earliest = RiderTripRules.EarliestDeparture(now, km, seats, settings.AverageSpeedKmh);
        if (input.DepartAt < earliest)
            return new BaseResponse<RideRequestRow>(default, ErrorCode.DepartureTooSoon);

        // Asking for a ride at an hour is the same commitment as holding a seat
        // at it, so it answers to the same clash rule.
        if (await riderAvailabilityService.CheckCanRide(riderId, input.DepartAt) is { } busy)
            return new BaseResponse<RideRequestRow>(default, busy);

        var request = new RideRequest
        {
            OriginAddress = input.Origin.Address,
            Origin = origin,
            DestinationAddress = input.Destination.Address,
            Destination = destination,
            Route = GeoFactory.Line(origin, destination),   // straight line in the MVP
            DepartAt = input.DepartAt,
            TimeWindowMinutes = WindowFor(input.TimeWindowMinutes),
            SeatsRequested = seats,
            RadiusMeters = MatchRules.RadiusFor(input.Nearby),
            DriverGenderPolicy = input.DriverGenderPolicy,
            GenderPolicy = input.CoRiderGenderPolicy,
            MinAge = input.MinAge,
            MaxAge = input.MaxAge,
            Status = RideRequestStatus.Open,
        };

        // The author is a participant like anybody else. That uniformity is what
        // makes "the request dies when the last rider leaves" a count rather than
        // a special case about whoever typed it in — and it is why both rows are
        // written in one transaction: a request whose seats belong to nobody
        // would be closed by the first person who left it.
        var participant = new RideRequestParticipant
        {
            RiderId = riderId,
            Rider = rider,
            Seats = seats,
            Status = RideRequestParticipantStatus.Active,
            CoRiderGenderPolicy = input.CoRiderGenderPolicy,
            MinAge = input.MinAge,
            MaxAge = input.MaxAge,
        };

        await unitOfWork.BeginTransactionAsync();
        try
        {
            requestRepository.Create(request);
            await unitOfWork.SaveAsync();

            participant.RideRequestId = request.Id;
            participantRepository.Create(participant);
            await unitOfWork.CommitAsync();
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }

        await auditService.LogAsync(AuditActions.RideRequestCreate, nameof(RideRequest), request.Id);

        // A ride needed within the hour interrupts drivers now; anything further
        // out waits on the board until the sweeper brings it into range.
        if (RiderTripRules.ShouldNotifyNow(request.DepartAt, now))
        {
            request.NotifiedAt = now;
            requestRepository.Update(request);
            await unitOfWork.SaveAsync();
            await notificationService.NotifyNearbyDrivers(request);
        }

        return new BaseResponse<RideRequestRow>(Row(request, [participant], rider, riderId, settings));
    }

    public async Task<BaseResponse<List<RideRequestRow>>> GetMine()
    {
        var riderId = securityManager.RequireUserId();
        var settings = await Settings();

        // Everything the caller is on, not everything they wrote: a rider who
        // joined somebody else's request is on it in every sense that matters.
        var mine = await participantRepository
            .Where(p => p.RiderId == riderId && p.Status == RideRequestParticipantStatus.Active)
            .Select(p => p.RideRequestId)
            .Distinct()
            .ToListAsync();

        var requests = await requestRepository
            .Where(r => mine.Contains(r.Id) && r.Status == RideRequestStatus.Open)
            .OrderByDescending(r => r.Id)
            .ToListAsync();

        return new BaseResponse<List<RideRequestRow>>(await Rows(requests, riderId, settings));
    }

    public async Task<BaseResponse<RideRequestRow>> Get(int id)
    {
        var callerId = securityManager.RequireUserId();
        var request = Load(id);
        if (request == null)
            return new BaseResponse<RideRequestRow>(default, ErrorCode.RideRequestNotFound);

        var participants = await ActiveParticipants(id);
        return new BaseResponse<RideRequestRow>(
            Row(request, participants, await Author(participants), callerId, await Settings(),
                await InterestCount(id), await HasOffered(id, callerId)));
    }

    /// <summary>
    /// Joins a request, taking seats on it.
    ///
    /// Two directions, both required. The joiner has to satisfy the conditions
    /// the pool already carries, and — the half that is easy to forget —
    /// everybody already on it has to satisfy the joiner's. Conditions therefore
    /// *intersect* as a pool grows, and a rider is never pushed out of a pool
    /// they were already in by somebody who arrived later. A pool whose
    /// membership can change under its members is the one outcome to rule out.
    /// </summary>
    public async Task<BaseResponse<RideRequestRow>> Join(int id, JoinRideRequestInput input)
    {
        var riderId = securityManager.RequireUserId();
        var rider = await userRepository.GetByIdAsync(riderId);
        if (rider == null) return new BaseResponse<RideRequestRow>(default, ErrorCode.NotFound);

        var request = Load(id);
        if (request == null)
            return new BaseResponse<RideRequestRow>(default, ErrorCode.RideRequestNotFound);
        if (!request.IsOpenAt(DateTime.UtcNow))
            return new BaseResponse<RideRequestRow>(default, ErrorCode.RideRequestNotOpen);

        var active = await ActiveParticipants(id);

        // Already on it: the same rider asking twice gets the request they are
        // already part of, not a second participation (§13.2).
        if (active.Any(p => p.RiderId == riderId))
            return new BaseResponse<RideRequestRow>(default, ErrorCode.AlreadyJoined);

        var seats = Math.Max(input.Seats, 1);
        var wanted = active.Sum(p => p.Seats);
        if (wanted + seats > RiderTripRules.MaxSeats)
            return new BaseResponse<RideRequestRow>(default, ErrorCode.RideRequestSeatsExceeded);

        if (await riderAvailabilityService.CheckCanRide(riderId, request.DepartAt) is { } busy)
            return new BaseResponse<RideRequestRow>(default, busy);

        // The joiner against the pool.
        if (RiderEligibilityRules.CheckRider(rider, request.Conditions, request.DepartAt) is { } refusal)
            return new BaseResponse<RideRequestRow>(default, refusal);

        // The pool against the joiner. Anybody already on it who would fail the
        // newcomer's conditions makes this join impossible — the newcomer is the
        // one who has to look elsewhere, because they are the one who has not
        // committed to anything yet.
        var incoming = input.Conditions;
        var aboard = await Riders(active);
        if (aboard.Any(other => !RiderEligibilityRules.CanRide(other, incoming, request.DepartAt)))
            return new BaseResponse<RideRequestRow>(default, ErrorCode.RideRequestConditionsConflict);

        // Who may drive is a different kind of clash: tightening it excludes no
        // rider already on the request, only candidate drivers. Two *opposed*
        // driver policies still leave nobody who could drive, and that is real.
        var driverPolicy = request.DriverGenderPolicy;
        if (input.DriverGenderPolicy != GenderPolicy.Any)
        {
            if (driverPolicy != GenderPolicy.Any && driverPolicy != input.DriverGenderPolicy)
                return new BaseResponse<RideRequestRow>(default, ErrorCode.RideRequestConditionsConflict);
            driverPolicy = input.DriverGenderPolicy;
        }

        var tightened = request.Conditions.Tighten(incoming);

        var joined = new RideRequestParticipant
        {
            RideRequestId = request.Id,
            RiderId = riderId,
            Seats = seats,
            Status = RideRequestParticipantStatus.Active,
            CoRiderGenderPolicy = input.CoRiderGenderPolicy,
            MinAge = input.MinAge,
            MaxAge = input.MaxAge,
        };
        participantRepository.Create(joined);

        request.SeatsRequested = wanted + seats;
        request.DriverGenderPolicy = driverPolicy;
        request.GenderPolicy = tightened.GenderPolicy;
        request.MinAge = tightened.MinAge;
        request.MaxAge = tightened.MaxAge;
        requestRepository.Update(request);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.RideRequestJoin, nameof(RideRequest), request.Id);

        // The riders already on it asked for this journey and are now nearer to
        // getting it — a pool of three is a far better proposition to a driver
        // than three asks for one seat, and they should be told it grew.
        var others = active.Select(p => p.RiderId).Where(r => r != riderId).Distinct().ToList();
        if (others.Count > 0)
        {
            await notificationService.NotifyMany(others, NotificationTemplate.RideRequestJoinedRider,
                args: new { origin = request.OriginAddress, destination = request.DestinationAddress },
                data: new { rideRequestId = request.Id });
        }

        List<RideRequestParticipant> now = [.. active, joined];
        return new BaseResponse<RideRequestRow>(
            Row(request, now, await Author(now), riderId, await Settings(),
                await InterestCount(id), false));
    }

    /// <summary>
    /// Gives the caller's seats back, and closes the request when they were the
    /// last ones.
    ///
    /// Only while it is still demand. Once a driver has been selected the seat
    /// is an ordinary booking on an ordinary trip, and leaving is that booking's
    /// own cancel — one verb to the rider, two code paths underneath, and this
    /// is the wrong one once there is a car.
    /// </summary>
    public async Task<BaseResponse> Leave(int id)
    {
        var riderId = securityManager.RequireUserId();

        var request = Load(id);
        if (request == null) return new BaseResponse(ErrorCode.RideRequestNotFound);
        if (request.Status != RideRequestStatus.Open)
            return new BaseResponse(ErrorCode.RideRequestNotOpen);

        var active = await ActiveParticipants(id);
        var mine = active.FirstOrDefault(p => p.RiderId == riderId);
        if (mine == null) return new BaseResponse(ErrorCode.RideRequestNotJoined);

        mine.Status = RideRequestParticipantStatus.Left;
        participantRepository.Update(mine);

        request.SeatsRequested = Math.Max(request.SeatsRequested - mine.Seats, 0);

        var abandoned = active.Count == 1;
        if (abandoned) request.Status = RideRequestStatus.Cancelled;
        requestRepository.Update(request);

        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.RideRequestLeave, nameof(RideRequest), request.Id);

        if (abandoned)
        {
            await auditService.LogAsync(AuditActions.RideRequestCancel, nameof(RideRequest), request.Id);

            // Every driver who offered for it is holding a card that can only
            // fail now, and the ones who were pushed it still have it on screen.
            await CloseInterests(id, DriverInterestStatus.Expired);
            await unitOfWork.SaveAsync();
            await notificationService.NotifyRideRequestClosed(request.Id, RiderTripClosedReason.Cancelled);
        }

        return new BaseResponse();
    }

    /// <summary>
    /// The driver's board.
    ///
    /// Everything on it, an offer must be able to honour: a driver out on the
    /// road sees nothing at all, a departure clashing with one of their own is
    /// dropped row by row, and a request whose riders asked for a different
    /// driver never appears. A board that shows what the API then refuses is
    /// worse than an empty one.
    /// </summary>
    public async Task<BaseResponse<List<RideRequestRow>>> GetNearby(double lat, double lng, int radiusMeters)
    {
        var driverId = securityManager.RequireUserId();
        var driver = await userRepository.GetByIdAsync(driverId);
        if (driver == null) return new BaseResponse<List<RideRequestRow>>(default, ErrorCode.NotFound);

        // A driver out on the road can take nothing at all — not tonight's
        // request either, by the same rule that stops them posting while engaged.
        if (await driverAvailabilityService.IsEngaged(driverId))
            return new BaseResponse<List<RideRequestRow>>([]);

        // The scheduling clash is per request rather than per driver: each
        // carries its own departure, so a driver busy at nine is refused the one
        // leaving at nine and still offered the one leaving at six.
        var committed = await driverAvailabilityService.CommittedDepartures(driverId);

        var origin = GeoFactory.Point(lat, lng);
        var radius = radiusMeters <= 0 ? MatchRules.NearRadiusMeters : radiusMeters;
        var now = DateTime.UtcNow;
        var driverPolicy = RiderEligibilityRules.PolicyFor(driver.Gender);

        // The caller's own requests drop out by participation rather than by an
        // author column: everybody on it is equally unable to drive it, not only
        // whoever wrote it first.
        var onIt = await participantRepository
            .Where(p => p.RiderId == driverId && p.Status == RideRequestParticipantStatus.Active)
            .Select(p => p.RideRequestId)
            .ToListAsync();

        // Two reaches, unioned: how far this driver is willing to look, and how
        // far the riders asked to be reached from. The second half is what makes
        // the board agree with the push — riders who chose "Anywhere" get a 50 km
        // reach, and a driver we already notified must be able to find the
        // request here even though their own filter is narrower.
        var requests = await requestRepository
            .Where(r => r.Status == RideRequestStatus.Open
                        && !onIt.Contains(r.Id)
                        && r.DepartAt > now
                        && (r.DriverGenderPolicy == GenderPolicy.Any
                            || r.DriverGenderPolicy == driverPolicy)
                        && (r.Origin.IsWithinDistance(origin, radius)
                            || r.Origin.Distance(origin) <= r.RadiusMeters))
            .OrderBy(r => r.Origin.Distance(origin))
            .Take(BoardSize)
            .ToListAsync();

        var free = requests
            .Where(r => !committed.Any(d => DriverAvailabilityRules.Clashes(
                d, MatchRules.DepartureFor(r.DepartAt, now))))
            .ToList();

        return new BaseResponse<List<RideRequestRow>>(await Rows(free, driverId, await Settings()));
    }

    /// <summary>
    /// Expires the requests whose departure came and went with nobody driving
    /// them.
    ///
    /// A request is **not** expired for failing to attract a driver — giving the
    /// marketplace time to find supply is the entire point of writing one — so
    /// the only deadline is its own departure.
    /// </summary>
    public async Task<int> ExpireDue()
    {
        var now = DateTime.UtcNow;
        var due = await requestRepository
            .Where(r => r.Status == RideRequestStatus.Open && r.DepartAt <= now)
            .ToListAsync();
        if (due.Count == 0) return 0;

        foreach (var request in due)
        {
            request.Status = RideRequestStatus.Expired;
            requestRepository.Update(request);
            await CloseInterests(request.Id, DriverInterestStatus.Expired);
        }
        await unitOfWork.SaveAsync();

        foreach (var request in due)
        {
            // No actor: the sweeper runs outside any request, so the log records
            // what happened and leaves ActorUserId null.
            await auditService.LogAsync(AuditActions.RideRequestExpire, nameof(RideRequest), request.Id);
            await notificationService.NotifyRideRequestClosed(request.Id, RiderTripClosedReason.Expired);
        }

        return due.Count;
    }

    public async Task<int> NotifyDue()
    {
        var now = DateTime.UtcNow;
        var horizon = now + RiderTripRules.NotifyLead;

        var due = await requestRepository
            .Where(r => r.Status == RideRequestStatus.Open
                        && r.NotifiedAt == null
                        && r.DepartAt > now
                        && r.DepartAt <= horizon)
            .ToListAsync();
        if (due.Count == 0) return 0;

        // Stamped before the push, and saved first: a delivery failure must not
        // leave the row looking un-notified, or every sweep from here on would
        // push it again. The notification is best-effort by design; the stamp is
        // the thing that has to be exactly once.
        foreach (var request in due)
        {
            request.NotifiedAt = now;
            requestRepository.Update(request);
        }
        await unitOfWork.SaveAsync();

        foreach (var request in due) await notificationService.NotifyNearbyDrivers(request);

        return due.Count;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private RideRequest? Load(int id) => requestRepository.FirstOrDefault(r => r.Id == id);

    /// <summary>
    /// The riders currently on a request. Read through the repository rather
    /// than off a navigation property — this service *mutates* these rows, and a
    /// missing Include would not fail here, it would silently find nobody and
    /// close a request people were on.
    /// </summary>
    private async Task<List<RideRequestParticipant>> ActiveParticipants(int requestId) =>
        await participantRepository
            .Where(p => p.RideRequestId == requestId
                        && p.Status == RideRequestParticipantStatus.Active)
            .ToListAsync();

    /// <summary>Marks every live offer on a closing request, so no driver is left holding one.</summary>
    private async Task CloseInterests(int requestId, DriverInterestStatus status)
    {
        var live = await interestRepository
            .Where(i => i.RideRequestId == requestId && i.Status == DriverInterestStatus.Interested)
            .ToListAsync();
        foreach (var interest in live)
        {
            interest.Status = status;
            interestRepository.Update(interest);
        }
    }

    private async Task<int> InterestCount(int requestId) =>
        await interestRepository.CountAsync(i =>
            i.RideRequestId == requestId && i.Status == DriverInterestStatus.Interested);

    private async Task<bool> HasOffered(int requestId, int driverId) =>
        await interestRepository.CountAsync(i =>
            i.RideRequestId == requestId
            && i.DriverId == driverId
            && i.Status == DriverInterestStatus.Interested) > 0;

    private async Task<List<User>> Riders(IReadOnlyCollection<RideRequestParticipant> participants)
    {
        var ids = participants.Select(p => p.RiderId).Distinct().ToList();
        if (ids.Count == 0) return [];
        return await userRepository.Where(u => ids.Contains(u.Id)).ToListAsync();
    }

    /// <summary>
    /// Whoever wrote it — the rider on the earliest participation. Looked up
    /// rather than stored, because being first confers nothing: they can leave,
    /// and the request outlives them while anybody else is still on it.
    /// </summary>
    private async Task<User?> Author(IReadOnlyCollection<RideRequestParticipant> participants)
    {
        var first = participants.OrderBy(p => p.Id).FirstOrDefault();
        return first == null ? null : await userRepository.GetByIdAsync(first.RiderId);
    }

    /// <summary>Rows for a page of requests, with every request's participants in one read.</summary>
    private async Task<List<RideRequestRow>> Rows(List<RideRequest> requests, int callerId,
        AppConfigurationOutput settings)
    {
        if (requests.Count == 0) return [];

        var ids = requests.Select(r => r.Id).ToList();
        var participants = await participantRepository
            .Where(p => ids.Contains(p.RideRequestId)
                        && p.Status == RideRequestParticipantStatus.Active)
            .ToListAsync();

        var riderIds = participants.Select(p => p.RiderId).Distinct().ToList();
        var riders = await userRepository.Where(u => riderIds.Contains(u.Id)).ToListAsync();

        var interests = await interestRepository
            .Where(i => ids.Contains(i.RideRequestId) && i.Status == DriverInterestStatus.Interested)
            .Select(i => new { i.RideRequestId, i.DriverId })
            .ToListAsync();

        return requests
            .Select(r =>
            {
                var onIt = participants.Where(p => p.RideRequestId == r.Id).ToList();
                var firstId = onIt.OrderBy(p => p.Id).FirstOrDefault()?.RiderId;
                var offers = interests.Where(i => i.RideRequestId == r.Id).ToList();
                return Row(r, onIt, riders.FirstOrDefault(u => u.Id == firstId), callerId, settings,
                    offers.Count, offers.Any(i => i.DriverId == callerId));
            })
            .ToList();
    }

    /// <summary>
    /// The settings, or the shipped defaults. <c>Get()</c> never fails, so a
    /// missing configuration row costs a request nothing.
    /// </summary>
    private async Task<AppConfigurationOutput> Settings() =>
        (await appConfigurationService.Get()).Data ?? new AppConfigurationOutput();

    /// <summary>
    /// The rider's acceptable departure window, or the marketplace's when they
    /// expressed none. Never a constant at the call site — that is exactly the
    /// hard-coded ±30 minutes v2 set out to remove.
    /// </summary>
    private static int WindowFor(int requested) =>
        requested > 0 ? Math.Clamp(requested, 1, 240) : (int)MatchRules.TimeWindow.TotalMinutes;

    private static RideRequestRow Row(RideRequest request,
        IReadOnlyCollection<RideRequestParticipant> participants, User? author, int callerId,
        AppConfigurationOutput settings, int interestCount = 0, bool callerHasOffered = false)
    {
        var km = GeoDistance.Km(request.Origin, request.Destination);
        var suggested = FareRules.PerSeat(km, settings.FareBaseAmount, settings.FarePerKm);
        return new RideRequestRow(request, participants, author, callerId, km, suggested,
            interestCount, callerHasOffered);
    }

    private static bool IsSamePoint(GeoPoint a, GeoPoint b) =>
        Math.Abs(a.Lat - b.Lat) < 1e-6 && Math.Abs(a.Lng - b.Lng) < 1e-6;
}
