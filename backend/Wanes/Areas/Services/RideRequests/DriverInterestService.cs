using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Series;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Configuration.Models;
using Wanes.Areas.Services.Marketplace;
using Wanes.Areas.Services.Marketplace.Models;
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
/// Where demand becomes supply: a driver offers, one offer is selected, and the
/// selected one is formed into a trip.
///
/// Three named steps for what a driver experiences as one tap on instant work.
/// With the instant window at zero — the shipped value — <see cref="ExpressInterest"/>
/// records the offer, selects it and forms the trip inside a single
/// transaction. Planned work (a request leaving later than
/// <see cref="DriverSelectionRules.InstantHorizon"/>) collects offers for the
/// scheduled window instead: the riders may compare and pick one
/// (<see cref="ChooseOffer"/>), and <see cref="SelectDue"/> decides whatever
/// they leave to the marketplace.
///
/// Every offer says the trip is shared (<see cref="ExpressInterestInput.AcceptSharedTrip"/>),
/// how many seats it puts on sale, and — for a conditional accept — how many
/// must be held before it runs.
/// </summary>
public class DriverInterestService : IDriverInterestService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IDriverAvailabilityService driverAvailabilityService;
    private readonly IAppConfigurationService appConfigurationService;
    private readonly IReliabilityService reliabilityService;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<TripStatusHistory> tripHistoryRepository;
    private readonly IRepository<Booking> bookingRepository;
    private readonly IRepository<RideRequest> requestRepository;
    private readonly IRepository<RideRequestParticipant> participantRepository;
    private readonly IRepository<DriverInterest> interestRepository;

    public DriverInterestService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IDriverAvailabilityService driverAvailabilityService,
        IAppConfigurationService appConfigurationService,
        IReliabilityService reliabilityService,
        IRepository<User> userRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<Trip> tripRepository,
        IRepository<TripStatusHistory> tripHistoryRepository,
        IRepository<Booking> bookingRepository,
        IRepository<RideRequest> requestRepository,
        IRepository<RideRequestParticipant> participantRepository,
        IRepository<DriverInterest> interestRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.driverAvailabilityService = driverAvailabilityService;
        this.appConfigurationService = appConfigurationService;
        this.reliabilityService = reliabilityService;
        this.userRepository = userRepository;
        this.vehicleRepository = vehicleRepository;
        this.tripRepository = tripRepository;
        this.tripHistoryRepository = tripHistoryRepository;
        this.bookingRepository = bookingRepository;
        this.requestRepository = requestRepository;
        this.participantRepository = participantRepository;
        this.interestRepository = interestRepository;
    }

    public async Task<BaseResponse<RideRequestRow>> ExpressInterest(
        int requestId, ExpressInterestInput? input = null)
    {
        var driverId = securityManager.RequireUserId();
        var settings = await Settings();

        // Idempotency first, before anything is staged. A double tap must yield
        // one offer — or one trip — and say so twice; refusing the second tap
        // with RideRequestNotOpen would be a lie to the driver who is holding
        // the ride they were just given.
        if (await Existing(requestId, driverId) is { } already) return already;

        // The shared-trip agreement. Asked before anything else about the
        // offer, because it is the promise every other rule leans on: the seats
        // this driver does not fill stay on sale, and more riders may join.
        if (settings.RequireSharedTermsAcceptance && input?.AcceptSharedTrip != true)
            return new BaseResponse<RideRequestRow>(default, ErrorCode.SharedTermsNotAccepted);

        var driver = await userRepository.GetByIdAsync(driverId);
        if (driver == null) return new BaseResponse<RideRequestRow>(default, ErrorCode.NotFound);
        if (driver.DriverStatus != DriverStatus.Verified)
            return new BaseResponse<RideRequestRow>(default, ErrorCode.DriverNotVerified);

        // No admin sign-off on vehicles: owning one is enough to offer.
        var vehicle = input?.VehicleId is { } chosen
            ? vehicleRepository.FirstOrDefault(v => v.Id == chosen && v.UserId == driverId)
            : vehicleRepository.Where(v => v.UserId == driverId)
                .OrderByDescending(v => v.IsDefault)
                .FirstOrDefault();
        if (vehicle == null) return new BaseResponse<RideRequestRow>(default, ErrorCode.VehicleNotFound);

        await unitOfWork.BeginTransactionAsync();
        try
        {
            var request = requestRepository.FirstOrDefault(r => r.Id == requestId);
            if (request == null) return await RollBack(ErrorCode.RideRequestNotFound);
            if (request.Status != RideRequestStatus.Open)
                return await RollBack(ErrorCode.RideRequestNotOpen);

            // The sweeper expires a request on a timer, so between a departure
            // passing and the next tick the row still reads Open. Honour the
            // clock, not the column.
            var now = DateTime.UtcNow;
            if (request.DepartAt <= now) return await RollBack(ErrorCode.RideRequestNotOpen);

            var participants = await ActiveParticipants(requestId);

            // Nobody drives themselves. The board already hides the caller's own
            // requests, but a board is a cache and this is the call that would
            // actually commit them to both sides of one journey.
            if (participants.Any(p => p.RiderId == driverId))
                return await RollBack(ErrorCode.CannotServeOwnRequest);

            // The riders' condition on who drives them.
            if (RiderEligibilityRules.CheckDriver(driver, request.DriverGenderPolicy) is { } notForYou)
                return await RollBack(notForYou);

            var wanted = participants.Sum(p => p.Seats);
            if (wanted > vehicle.SeatCapacity) return await RollBack(ErrorCode.SeatsExceedCapacity);

            // Seats on sale: at least the pool's, at most the car's.
            var seatsOffered = input?.SeatsOffered ?? vehicle.SeatCapacity;
            if (seatsOffered < wanted || seatsOffered > vehicle.SeatCapacity)
                return await RollBack(ErrorCode.InvalidSeatsOffered);

            // What the driver is committing to. The request carries the
            // departure its riders want, so this is not "now" — and availability
            // has to be asked about that moment, or a driver free all evening
            // would be refused tonight's request because of one they are running
            // right now.
            var departAt = MatchRules.DepartureFor(request.DepartAt, now);
            if (await driverAvailabilityService.CheckCanCommit(driverId, departAt) is { } busy)
                return await RollBack(busy);

            // A driver whose record paused instant work can still plan ahead.
            if (reliabilityService.CheckCanTake(driver, departAt, now) is { } paused)
                return await RollBack(paused);

            var interest = new DriverInterest
            {
                RideRequestId = request.Id,
                DriverId = driverId,
                Driver = driver,
                VehicleId = vehicle.Id,
                Vehicle = vehicle,
                Status = DriverInterestStatus.Interested,
                Message = string.IsNullOrWhiteSpace(input?.Message) ? null : input!.Message!.Trim(),
                SeatsOffered = seatsOffered,
                MinPassengers = input?.MinPassengers is { } min && min > wanted
                    ? Math.Min(min, seatsOffered)
                    : null,
                SharedTermsAcceptedAt = input?.AcceptSharedTrip == true ? now : null,

                // Verbatim if the driver named a price, derived from the distance
                // if they somehow did not. Either way the trip this becomes
                // carries a figure: it is offered in search beside trips that all
                // show one, and a blank there reads as free rather than unset.
                PricePerSeat = input?.PricePerSeat is { } price
                    ? FareRules.PriceFor(price)
                    : FareRules.PerSeat(
                        GeoDistance.Km(request.Origin, request.Destination),
                        settings.FareBaseAmount,
                        settings.FarePerKm),
            };
            interestRepository.Create(interest);

            // Stamped on the first offer only — it is when the window opens, not
            // when the latest driver happened to arrive.
            if (request.FirstInterestAt == null)
            {
                request.FirstInterestAt = now;
                request.DecideAt = DriverSelectionRules.DecideAt(now, request.DepartAt,
                    settings.DriverSelectionWindowMinutes, settings.ScheduledSelectionWindowMinutes);
            }
            request.DecideAt ??= request.FirstInterestAt;
            requestRepository.Update(request);

            var immediate = request.DecideAt <= now;
            Trip? trip = null;
            if (immediate)
            {
                // First-come-first-served, in one place: this offer is the only
                // live one, so it is the best one, and the request is decided
                // now. Inside the same transaction as the interest, and
                // conditional on the request's row version — which is what makes
                // "first wins" true rather than hoped for.
                trip = await Form(request, interest, participants, driver, vehicle, now);
            }

            await unitOfWork.CommitAsync();
            await auditService.LogAsync(AuditActions.DriverInterest, nameof(DriverInterest), interest.Id);

            if (trip != null)
            {
                await AfterFormation(request, trip, driver, interest, participants);
            }
            else
            {
                // Offers are accumulating. The riders hear that somebody is
                // willing — the one thing that visibly moves while they wait.
                await notificationService.NotifyMany(
                    participants.Select(p => p.RiderId).Distinct().ToList(),
                    NotificationTemplate.RideRequestInterestRider,
                    args: new
                    {
                        name = driver.FirstName,
                        origin = request.OriginAddress,
                        destination = request.DestinationAddress,
                    },
                    data: new { rideRequestId = request.Id, decideAt = request.DecideAt });
            }

            return await RowFor(request, participants, driverId, settings);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another driver was selected first. Nothing of this attempt
            // survives — including the trip and bookings it had staged.
            await unitOfWork.RollBackAsync();
            unitOfWork.Detach();
            return new BaseResponse<RideRequestRow>(default, ErrorCode.RideRequestNotOpen);
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }
    }

    public async Task<BaseResponse> WithdrawInterest(int requestId)
    {
        var driverId = securityManager.RequireUserId();

        var interest = interestRepository.FirstOrDefault(i =>
            i.RideRequestId == requestId
            && i.DriverId == driverId
            && i.Status == DriverInterestStatus.Interested);
        if (interest == null) return new BaseResponse(ErrorCode.DriverInterestNotFound);

        var request = requestRepository.FirstOrDefault(r => r.Id == requestId);
        if (request is not { Status: RideRequestStatus.Open })
            return new BaseResponse(ErrorCode.RideRequestNotOpen);

        interest.Status = DriverInterestStatus.Withdrawn;
        interestRepository.Update(interest);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.DriverWithdraw, nameof(DriverInterest), interest.Id);

        return new BaseResponse();
    }

    public async Task<BaseResponse<List<OfferRow>>> GetOffers(int requestId)
    {
        var callerId = securityManager.RequireUserId();
        var request = requestRepository.FirstOrDefault(r => r.Id == requestId);
        if (request == null) return new BaseResponse<List<OfferRow>>(default, ErrorCode.RideRequestNotFound);

        // The offers are the riders' to compare, and nobody else's.
        var participants = await ActiveParticipants(requestId);
        if (participants.All(p => p.RiderId != callerId))
            return new BaseResponse<List<OfferRow>>(default, ErrorCode.Forbidden);

        var offers = await interestRepository
            .Where(i => i.RideRequestId == requestId && i.Status == DriverInterestStatus.Interested,
                q => q.Include(i => i.Driver).Include(i => i.Vehicle))
            .OrderBy(i => i.PricePerSeat)
            .ThenBy(i => i.Id)
            .ToListAsync();
        return new BaseResponse<List<OfferRow>>(offers.Select(i => new OfferRow(i)).ToList());
    }

    public async Task<BaseResponse<RideRequestRow>> ChooseOffer(int requestId, int interestId)
    {
        var callerId = securityManager.RequireUserId();
        var settings = await Settings();
        if (!settings.RiderOfferChoice)
            return new BaseResponse<RideRequestRow>(default, ErrorCode.OfferChoiceNotAvailable);

        var request = requestRepository.FirstOrDefault(r => r.Id == requestId);
        if (request == null) return new BaseResponse<RideRequestRow>(default, ErrorCode.RideRequestNotFound);

        var now = DateTime.UtcNow;
        var participants = await ActiveParticipants(requestId);
        if (participants.All(p => p.RiderId != callerId))
            return new BaseResponse<RideRequestRow>(default, ErrorCode.Forbidden);
        if (!request.IsOpenAt(now))
            return new BaseResponse<RideRequestRow>(default, ErrorCode.RideRequestNotOpen);

        var offer = interestRepository.FirstOrDefault(i =>
            i.Id == interestId && i.RideRequestId == requestId && i.Status == DriverInterestStatus.Interested);
        if (offer == null) return new BaseResponse<RideRequestRow>(default, ErrorCode.OfferNotAvailable);

        var outcome = await Decide(request, now, forcedInterestId: offer.Id);
        if (outcome == null) return new BaseResponse<RideRequestRow>(default, ErrorCode.OfferNotAvailable);

        await auditService.LogAsync(AuditActions.OfferChosen, nameof(DriverInterest), offer.Id);
        await notificationService.Notify(outcome.Value.DriverId, NotificationTemplate.OfferChosenDriver,
            args: new { origin = request.OriginAddress, destination = request.DestinationAddress },
            data: new { rideRequestId = request.Id, tripId = outcome.Value.TripId });

        return await RowFor(request, participants, callerId, settings);
    }

    public async Task<int> SelectDue()
    {
        var now = DateTime.UtcNow;

        // Requests whose offers are due a decision. Instant ones were decided
        // inside the call that made the offer and are no longer open, so this
        // only ever finds planned work — and any legacy row whose window the
        // admin has since changed.
        var due = await requestRepository
            .Where(r => r.Status == RideRequestStatus.Open
                        && r.FirstInterestAt != null
                        && r.DecideAt != null
                        && r.DecideAt <= now
                        && r.DepartAt > now)
            .ToListAsync();
        if (due.Count == 0) return 0;

        var matched = 0;
        foreach (var request in due)
        {
            if (await Decide(request, now) != null) matched++;
        }

        return matched;
    }

    public async Task<SeriesDayOutcome> FormForSeries(int requestId, SeriesCommitment commitment)
    {
        var now = DateTime.UtcNow;
        var settings = await Settings();

        var driver = await userRepository.GetByIdAsync(commitment.DriverId);
        if (driver is not { DriverStatus: DriverStatus.Verified } || driver.IsDisabled)
            return new SeriesDayOutcome(null, ErrorCode.DriverNotVerified);

        var vehicle = commitment.VehicleId is { } vehicleId
            ? vehicleRepository.FirstOrDefault(v => v.Id == vehicleId && v.UserId == driver.Id)
            : null;
        vehicle ??= vehicleRepository.Where(v => v.UserId == driver.Id)
            .OrderByDescending(v => v.IsDefault)
            .FirstOrDefault();
        if (vehicle == null) return new SeriesDayOutcome(null, ErrorCode.VehicleNotFound);

        await unitOfWork.BeginTransactionAsync();
        try
        {
            var request = requestRepository.FirstOrDefault(r => r.Id == requestId);
            if (request == null || !request.IsOpenAt(now))
                return await Refuse(ErrorCode.RideRequestNotOpen);

            var participants = await ActiveParticipants(requestId);
            if (participants.Count == 0) return await Refuse(ErrorCode.RideRequestNotOpen);
            if (participants.Any(p => p.RiderId == driver.Id)) return await Refuse(ErrorCode.CannotServeOwnRequest);
            if (RiderEligibilityRules.CheckDriver(driver, request.DriverGenderPolicy) is { } notForYou)
                return await Refuse(notForYou);

            var wanted = participants.Sum(p => p.Seats);
            if (wanted > vehicle.SeatCapacity) return await Refuse(ErrorCode.SeatsExceedCapacity);

            // A future day asks only about that day: whatever the driver is
            // doing right now has finished long before it.
            var departAt = MatchRules.DepartureFor(request.DepartAt, now);
            var promised = await driverAvailabilityService.CommittedDepartures(driver.Id);
            if (promised.Any(d => DriverAvailabilityRules.Clashes(d, departAt)))
                return await Refuse(ErrorCode.DriverTripTimeConflict);

            // The series driver takes the day outright. Anyone who offered for
            // this one day is answered, as on any other decision.
            var live = await interestRepository
                .Where(i => i.RideRequestId == request.Id && i.Status == DriverInterestStatus.Interested)
                .ToListAsync();
            var mine = live.FirstOrDefault(i => i.DriverId == driver.Id);

            var seats = Math.Clamp(commitment.Seats ?? vehicle.SeatCapacity, wanted, vehicle.SeatCapacity);
            var interest = mine ?? new DriverInterest
            {
                RideRequestId = request.Id,
                DriverId = driver.Id,
                Status = DriverInterestStatus.Interested,
            };
            interest.Driver = driver;
            interest.VehicleId = vehicle.Id;
            interest.Vehicle = vehicle;
            interest.SeatsOffered = seats;
            interest.MinPassengers = null;
            interest.Message = commitment.Message;
            interest.SharedTermsAcceptedAt = commitment.SharedTermsAcceptedAt;
            interest.PricePerSeat = commitment.PricePerSeat is { } price
                ? FareRules.PriceFor(price)
                : FareRules.PerSeat(GeoDistance.Km(request.Origin, request.Destination),
                    settings.FareBaseAmount, settings.FarePerKm);
            if (mine == null) interestRepository.Create(interest);
            else interestRepository.Update(interest);

            request.FirstInterestAt ??= now;
            request.DecideAt ??= now;

            var trip = await Form(request, interest, participants, driver, vehicle, now);
            // The day it is, so every message about it can name it. No
            // ScheduleId: the schedule is the rider's, and the trip is not one
            // of its generated rows.
            trip.SeriesCommitmentId = commitment.Id;
            trip.OccurrenceDate = request.OccurrenceDate;
            tripRepository.Update(trip);

            var losers = live.Where(i => i.Id != interest.Id).ToList();
            foreach (var loser in losers)
            {
                loser.Status = DriverSelectionRules.LosingStatus;
                interestRepository.Update(loser);
            }

            await unitOfWork.CommitAsync();

            // One push a day per rider would be noise on a series they already
            // agreed to; the week-ahead summary carries the good news.
            await AfterFormation(request, trip, driver, interest, participants, quiet: true);
            await NotifyLosers(request, losers);
            return new SeriesDayOutcome(trip.Id, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            await unitOfWork.RollBackAsync();
            unitOfWork.Detach();
            return new SeriesDayOutcome(null, ErrorCode.RideRequestNotOpen);
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }

        async Task<SeriesDayOutcome> Refuse(ErrorCode code)
        {
            await unitOfWork.RollBackAsync();
            return new SeriesDayOutcome(null, code);
        }
    }

    // ── Selection and formation ──────────────────────────────────────────────

    /// <summary>
    /// Picks the best live offer on one request — or the one its riders chose —
    /// and forms the trip. One transaction per request rather than one for the
    /// sweep: a pool whose chosen driver turns out to be busy must not take the
    /// rest of the batch down with it.
    /// </summary>
    private async Task<(int DriverId, int TripId)?> Decide(RideRequest request, DateTime now,
        int? forcedInterestId = null)
    {
        await unitOfWork.BeginTransactionAsync();
        try
        {
            var live = await interestRepository
                .Where(i => i.RideRequestId == request.Id
                            && i.Status == DriverInterestStatus.Interested)
                .ToListAsync();
            if (live.Count == 0)
            {
                await unitOfWork.RollBackAsync();
                return null;
            }

            var participants = await ActiveParticipants(request.Id);
            if (participants.Count == 0)
            {
                await unitOfWork.RollBackAsync();
                return null;
            }

            var driverIds = live.Select(i => i.DriverId).Distinct().ToList();
            var drivers = await userRepository.Where(u => driverIds.Contains(u.Id)).ToListAsync();
            var vehicleIds = live.Select(i => i.VehicleId).Distinct().ToList();
            var vehicles = await vehicleRepository.Where(v => vehicleIds.Contains(v.Id)).ToListAsync();
            foreach (var interest in live)
                interest.Vehicle ??= vehicles.FirstOrDefault(v => v.Id == interest.VehicleId);

            // A driver who offered an hour ago may since have committed to
            // something else. Asked again here rather than trusted from the
            // offer, because the offer is a statement about intent and this is
            // the moment it becomes a commitment.
            var wanted = participants.Sum(p => p.Seats);
            var departAt = MatchRules.DepartureFor(request.DepartAt, now);
            var busy = await driverAvailabilityService.BusyDrivers(driverIds, departAt);
            var eligible = live
                .Where(i => !busy.Contains(i.DriverId))
                // A pool that grew past an offer's seats cannot be carried by it.
                .Where(i => i.SeatsOffered == null || i.SeatsOffered >= wanted)
                .ToList();

            var winner = forcedInterestId is { } forced
                ? eligible.FirstOrDefault(i => i.Id == forced)
                : DriverSelectionRules.Best(eligible, drivers.ToDictionary(d => d.Id), wanted);
            if (winner == null)
            {
                await unitOfWork.RollBackAsync();
                return null;
            }

            var driver = drivers.First(d => d.Id == winner.DriverId);
            var vehicle = winner.Vehicle ?? vehicles.First(v => v.Id == winner.VehicleId);

            var trip = await Form(request, winner, participants, driver, vehicle, now);

            // Every other offer is answered. Silence would leave a driver unable
            // to tell "somebody else got it" from "the app is broken".
            var losers = live.Where(i => i.Id != winner.Id).ToList();
            foreach (var loser in losers)
            {
                loser.Status = DriverSelectionRules.LosingStatus;
                interestRepository.Update(loser);
            }

            await unitOfWork.CommitAsync();
            await AfterFormation(request, trip, driver, winner, participants);
            await NotifyLosers(request, losers);
            return (driver.Id, trip.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            await unitOfWork.RollBackAsync();
            unitOfWork.Detach();
            return null;
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }
    }

    /// <summary>
    /// Demand becomes supply.
    ///
    /// The rule that matters is in the loop: **each participant becomes exactly
    /// one booking**, carrying their seats. No duplicates, no second pass,
    /// nobody dropped — a conversion that lost a rider would put somebody at a
    /// kerb the driver was never told about.
    ///
    /// The trip is confirmed at formation unless the driver accepted on a
    /// condition the pool does not meet yet. Its passengers are secured: they
    /// asked for this ride, a driver is making it, and the only new fact is the
    /// price — which a rider who dislikes it answers by leaving, at no cost. A
    /// conditional accept is the one exception, and it is the ordinary
    /// gathering trip every other rule already understands.
    ///
    /// Staged inside the caller's transaction, and conditional on the request's
    /// row version, so two drivers cannot both form a trip from one request.
    /// </summary>
    private async Task<Trip> Form(
        RideRequest request,
        DriverInterest winner,
        IReadOnlyCollection<RideRequestParticipant> participants,
        User driver,
        Vehicle vehicle,
        DateTime now)
    {
        var wanted = participants.Sum(p => p.Seats);
        var departAt = MatchRules.DepartureFor(request.DepartAt, now);

        // Seats come from the driver's offer, bounded by the car: the spare ones
        // are ordinary carpool seats, searchable and bookable by anybody,
        // because after formation this is an ordinary trip in every respect.
        var seatsTotal = Math.Clamp(winner.SeatsOffered ?? vehicle.SeatCapacity, wanted, vehicle.SeatCapacity);

        var threshold = winner.MinPassengers is { } min
            ? TripConfirmationRules.ThresholdFor(min, seatsTotal)
            : TripConfirmationRules.NoThreshold;
        var confirmed = TripConfirmationRules.IsMet(threshold, wanted);

        var trip = new Trip
        {
            DriverId = driver.Id,
            VehicleId = vehicle.Id,
            OriginAddress = request.OriginAddress,
            Origin = request.Origin,
            DestinationAddress = request.DestinationAddress,
            Destination = request.Destination,
            Route = request.Route ?? GeoFactory.Line(request.Origin, request.Destination),
            DepartAt = departAt,
            SeatsTotal = seatsTotal,
            SeatsLeft = Math.Max(seatsTotal - wanted, 0),
            PricePerSeat = winner.PricePerSeat,

            // The riders' conditions come with them. A pool that asked to share
            // with women only keeps that condition over the spare seats, which
            // is what they agreed to.
            GenderPolicy = request.GenderPolicy,
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,

            MinSeatsToConfirm = confirmed ? TripConfirmationRules.NoThreshold : threshold,
            ConfirmedAt = confirmed ? now : null,
            Status = TripStatus.Posted,
        };
        tripRepository.Create(trip);
        await unitOfWork.SaveAsync();

        tripHistoryRepository.Create(new TripStatusHistory
        {
            TripId = trip.Id,
            Status = TripStatus.Posted,
            ChangedBy = driver.Id,
        });

        foreach (var participant in participants)
        {
            bookingRepository.Create(new Booking
            {
                TripId = trip.Id,
                RiderId = participant.RiderId,
                Seats = participant.Seats,
                Status = confirmed ? BookingStatus.Confirmed : BookingStatus.Pending,
                CoRiderGenderPolicy = participant.CoRiderGenderPolicy,
                MinAge = participant.MinAge,
                MaxAge = participant.MaxAge,
                BoardingCode = BoardingCodes.New(),
                SharedTermsAcceptedAt = participant.SharedTermsAcceptedAt,
            });
        }

        winner.Status = DriverInterestStatus.Selected;
        interestRepository.Update(winner);

        request.Status = RideRequestStatus.Matched;
        request.MatchedTripId = trip.Id;
        requestRepository.Update(request);

        return trip;
    }

    /// <summary>
    /// Everything that happens once formation is committed: the audit trail, the
    /// riders, the drivers still holding a card, and the riders elsewhere who
    /// could use the spare seats.
    ///
    /// Deliberately after the commit and best-effort. A failed push must not
    /// undo a trip that exists.
    /// </summary>
    private async Task AfterFormation(
        RideRequest request,
        Trip trip,
        User driver,
        DriverInterest winner,
        IReadOnlyCollection<RideRequestParticipant> participants,
        bool quiet = false)
    {
        await auditService.LogAsync(AuditActions.DriverSelect, nameof(RideRequest), request.Id);
        await auditService.LogAsync(AuditActions.TripForm, nameof(Trip), trip.Id);

        // The riders learn two things at once: they have a driver, and their ride
        // now has an id of its own. The trip id is what their screens follow from
        // here — the request has done its job.
        var gathering = !trip.IsConfirmed;
        if (!quiet)
            await notificationService.NotifyMany(
            participants.Select(p => p.RiderId).Distinct().ToList(),
            gathering ? NotificationTemplate.RideRequestMatchedGatheringRider : NotificationTemplate.RideRequestMatchedRider,
            args: new { name = driver.FirstName, min = trip.MinSeatsToConfirm },
            data: new
            {
                rideRequestId = request.Id,
                tripId = trip.Id,
                pricePerSeat = trip.PricePerSeat,
                gathering,
            });

        // Every driver who was offered this request is now holding a card that
        // can only fail.
        await notificationService.NotifyRideRequestClosed(request.Id, RiderTripClosedReason.Claimed);

        // The spare seats are carpool seats like any other, and riders on their
        // own open requests along this route can have them.
        if (trip.SeatsLeft > 0) await notificationService.NotifyWaitingRiders(trip, driver);

        _ = winner;
    }

    private async Task NotifyLosers(RideRequest request, IReadOnlyCollection<DriverInterest> losers)
    {
        if (losers.Count == 0) return;
        await notificationService.NotifyMany(
            losers.Select(i => i.DriverId).Distinct().ToList(),
            NotificationTemplate.RideRequestNotSelectedDriver,
            args: new { origin = request.OriginAddress, destination = request.DestinationAddress },
            data: new { rideRequestId = request.Id });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// What to hand back when this driver has already answered this request —
    /// their standing offer, or the trip it became — and <c>null</c> when they
    /// have not.
    ///
    /// Deliberately narrow: only the driver who made the offer is answered this
    /// way. Another driver asking about a request that is already matched is a
    /// loser, not a repeat caller, and belongs on the refusal path.
    /// </summary>
    private async Task<BaseResponse<RideRequestRow>?> Existing(int requestId, int driverId)
    {
        var mine = interestRepository.FirstOrDefault(i =>
            i.RideRequestId == requestId
            && i.DriverId == driverId
            && (i.Status == DriverInterestStatus.Interested
                || i.Status == DriverInterestStatus.Selected));
        if (mine == null) return null;

        var request = requestRepository.FirstOrDefault(r => r.Id == requestId);
        if (request == null) return null;

        return await RowFor(request, await ActiveParticipants(requestId), driverId, await Settings());
    }

    private async Task<List<RideRequestParticipant>> ActiveParticipants(int requestId) =>
        await participantRepository
            .Where(p => p.RideRequestId == requestId
                        && p.Status == RideRequestParticipantStatus.Active)
            .ToListAsync();

    private async Task<BaseResponse<RideRequestRow>> RowFor(
        RideRequest request,
        IReadOnlyCollection<RideRequestParticipant> participants,
        int callerId,
        AppConfigurationOutput settings)
    {
        var first = participants.OrderBy(p => p.Id).FirstOrDefault();
        var author = first == null ? null : await userRepository.GetByIdAsync(first.RiderId);

        var offers = await interestRepository
            .Where(i => i.RideRequestId == request.Id && i.Status == DriverInterestStatus.Interested)
            .Select(i => i.DriverId)
            .ToListAsync();

        var km = GeoDistance.Km(request.Origin, request.Destination);
        var suggested = FareRules.PerSeat(km, settings.FareBaseAmount, settings.FarePerKm);

        return new BaseResponse<RideRequestRow>(new RideRequestRow(
            request, participants, author, callerId, km, suggested,
            offers.Count, offers.Contains(callerId)));
    }

    private async Task<AppConfigurationOutput> Settings() =>
        (await appConfigurationService.Get()).Data ?? new AppConfigurationOutput();

    private async Task<BaseResponse<RideRequestRow>> RollBack(ErrorCode errorCode)
    {
        await unitOfWork.RollBackAsync();
        return new BaseResponse<RideRequestRow>(default, errorCode);
    }
}
