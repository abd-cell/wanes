using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.RideRequests.Models;
using Wanes.Areas.Services.Search.Models;
using Wanes.Areas.Services.Users.Availability;
using Wanes.DataAccess.Repositories;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Search;

/// <summary>
/// A driver searching for riders — the mirror of <see cref="SearchService"/>.
///
/// Same two bands, same corridor maths, roles swapped. A rider asks "whose route
/// passes me?" and projects their own two ends onto each driver's route; a
/// driver asks "who is going my way?" and projects each request's two ends onto
/// their own. One <see cref="RouteGeometry"/> serves both, which is the point of
/// it living in <c>Shareds</c>: two notions of "on the way" would drift.
///
/// What is *not* shared is eligibility. A driver's list is bounded by their car,
/// their verification and their diary; a rider's by seats and the driver's
/// conditions. Those are different questions and they live in different classes.
/// </summary>
public class DemandSearchService : IDemandSearchService
{
    /// <summary>How many trips one page shows, across both bands.</summary>
    private const int MaxResults = 20;

    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly IAppConfigurationService appConfigurationService;
    private readonly IDriverAvailabilityService driverAvailabilityService;
    private readonly IRepository<RideRequest> requestRepository;
    private readonly IRepository<RideRequestParticipant> participantRepository;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<Vehicle> vehicleRepository;

    public DemandSearchService(
        ISecurityManager securityManager,
        IAuditService auditService,
        IAppConfigurationService appConfigurationService,
        IDriverAvailabilityService driverAvailabilityService,
        IRepository<RideRequest> requestRepository,
        IRepository<RideRequestParticipant> participantRepository,
        IRepository<User> userRepository,
        IRepository<Vehicle> vehicleRepository)
    {
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.appConfigurationService = appConfigurationService;
        this.driverAvailabilityService = driverAvailabilityService;
        this.requestRepository = requestRepository;
        this.participantRepository = participantRepository;
        this.userRepository = userRepository;
        this.vehicleRepository = vehicleRepository;
    }

    public async Task<BaseResponse<DemandSearchResult>> Search(DemandSearchInput input)
    {
        var driverId = securityManager.RequireUserId();
        var driver = await userRepository.GetByIdAsync(driverId);
        if (driver == null) return new BaseResponse<DemandSearchResult>(default, ErrorCode.NotFound);
        if (driver.DriverStatus != DriverStatus.Verified)
            return new BaseResponse<DemandSearchResult>(default, ErrorCode.DriverNotVerified);

        if (IsSamePoint(input.Origin, input.Destination))
            return new BaseResponse<DemandSearchResult>(default, ErrorCode.OriginEqualsDestination);

        // Seats they can offer. Read off the car unless they said otherwise, so
        // the common case needs no input — and capped by the car either way: a
        // driver cannot offer seats they do not have.
        var capacity = vehicleRepository.Where(v => v.UserId == driverId)
            .OrderByDescending(v => v.IsDefault)
            .Select(v => (int?)v.SeatCapacity)
            .FirstOrDefault();
        if (capacity == null) return new BaseResponse<DemandSearchResult>(default, ErrorCode.VehicleNotFound);

        var seats = input.Seats > 0 ? Math.Min(input.Seats, capacity.Value) : capacity.Value;

        var result = new DemandSearchResult { SeatsOffered = seats };

        // A driver already out on the road can take nothing at all, so the
        // honest answer is an empty list rather than one they cannot act on.
        if (await driverAvailabilityService.IsEngaged(driverId))
            return new BaseResponse<DemandSearchResult>(result);

        var origin = GeoFactory.Point(input.Origin.Lat, input.Origin.Lng);
        var destination = GeoFactory.Point(input.Destination.Lat, input.Destination.Lng);
        var route = GeoFactory.Line(origin, destination);
        var matchRadius = MatchRules.RadiusFor(input.Nearby);
        var now = DateTime.UtcNow;

        var candidates = await Takeable(driver, input, route, seats, matchRadius, now);
        candidates = await AdmittedBy(driver, input.RiderGenderPolicy, candidates);

        // Tier 1 first, and its ids keep a request out of tier 2: one whose own
        // ends sit near the driver's is a direct match, and offering it again as
        // "on the way" would be the same row twice.
        var direct = Direct(candidates, origin, destination, matchRadius);
        var directIds = direct.Select(m => m.Request.Id).ToHashSet();
        var corridor = Corridor(candidates, route, directIds);

        var committed = await driverAvailabilityService.CommittedDepartures(driverId);
        var ranked = Rank(direct, corridor, input, matchRadius)
            // The diary, per request rather than per driver: each carries its own
            // departure, so a driver busy at nine is still free for the six.
            .Where(m => !committed.Any(d => DriverAvailabilityRules.Clashes(
                d, MatchRules.DepartureFor(m.Request.DepartAt, now))))
            .Take(MaxResults)
            .ToList();

        await Fill(ranked);
        result.Matches = ranked;

        await auditService.LogAsync(AuditActions.SearchDemand, nameof(RideRequest));
        return new BaseResponse<DemandSearchResult>(result);
    }

    /// <summary>
    /// Requests this driver could actually serve, before either band's geometry
    /// is considered: open, in the future, small enough for the car, and
    /// admitting this driver.
    ///
    /// One query for both tiers, for the same reason the rider's side runs one:
    /// they differ only in *where* the request has to be, and running the same
    /// predicates twice is where two lists drift apart.
    /// </summary>
    private async Task<List<RideRequest>> Takeable(User driver, DemandSearchInput input,
        LineString route, int seats, int matchRadius, DateTime now)
    {
        // Nobody drives a request they are on — read off their participations,
        // so everybody on it is excluded and not only whoever wrote it.
        var onIt = await participantRepository
            .Where(p => p.RiderId == driver.Id
                        && p.Status == RideRequestParticipantStatus.Active)
            .Select(p => p.RideRequestId)
            .ToListAsync();

        var driverPolicy = RiderEligibilityRules.PolicyFor(driver.Gender);
        var from = input.When - MatchRules.TimeWindow;
        var to = input.When + MatchRules.TimeWindow;

        // A generous prefilter in SQL; the bands narrow it in memory.
        //
        // Bounded against the *middle* of the driver's route, not its start. A
        // corridor match begins somewhere along the way by definition, so a
        // radius around the driver's origin would throw out precisely the band
        // it was meant to feed. Half the route plus the match radius is the
        // smallest circle that certainly contains both bands.
        var midpoint = RouteGeometry.Midpoint(route);
        var reachMeters = (int)(RouteGeometry.LengthKm(route) * 1000 / 2) + matchRadius;

        return await requestRepository
            .Where(r => r.Status == RideRequestStatus.Open
                        && r.DepartAt > now
                        && !onIt.Contains(r.Id)
                        && r.SeatsRequested <= seats
                        && r.SeatsRequested >= input.MinSeats
                        // The riders' condition on who drives them.
                        && (r.DriverGenderPolicy == GenderPolicy.Any
                            || r.DriverGenderPolicy == driverPolicy)
                        && r.DepartAt >= from && r.DepartAt <= to
                        && r.Origin.IsWithinDistance(midpoint, reachMeters))
            .OrderBy(r => r.Origin.Distance(midpoint))
            .Take(MatchRules.CandidatePool)
            .ToListAsync();
    }

    /// <summary>
    /// The driver's own condition on who they carry, applied to the candidates.
    ///
    /// In memory rather than in the query, because it is a question about the
    /// *people on* each request rather than about the request itself. The
    /// candidate pool is bounded, so this is one extra read over it.
    ///
    /// A pool that has stated the same condition passes on the column alone. One
    /// that has stated nothing is judged on who is actually aboard: an unstated
    /// condition excludes nobody, but a driver who asked to carry women only
    /// should not be shown a pool with a man in it.
    /// </summary>
    private async Task<List<RideRequest>> AdmittedBy(User driver, GenderPolicy wanted,
        List<RideRequest> candidates)
    {
        if (wanted == GenderPolicy.Any || candidates.Count == 0) return candidates;

        var ids = candidates.Select(r => r.Id).ToList();
        var members = await participantRepository
            .Where(p => ids.Contains(p.RideRequestId)
                        && p.Status == RideRequestParticipantStatus.Active)
            .Select(p => new { p.RideRequestId, p.RiderId })
            .ToListAsync();
        var riderIds = members.Select(p => p.RiderId).Distinct().ToList();
        var genderOf = (await userRepository.Where(u => riderIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Gender })
                .ToListAsync())
            .ToDictionary(u => u.Id, u => u.Gender);

        return candidates
            .Where(r => r.GenderPolicy == wanted
                        || members.Where(p => p.RideRequestId == r.Id).All(p =>
                            genderOf.TryGetValue(p.RiderId, out var g)
                            && RiderEligibilityRules.Admits(wanted, g)))
            .ToList();
    }

    /// <summary>
    /// Tier 1 — the request's two ends sit near the driver's own. They want the
    /// journey the driver was already making, and the diversion is to a place
    /// the driver meant to be at anyway.
    /// </summary>
    private static List<(RideRequest Request, double Pickup, double Dropoff)> Direct(
        List<RideRequest> candidates, Point origin, Point destination, int matchRadiusMeters)
    {
        var radiusKm = matchRadiusMeters / 1000.0;
        var matches = new List<(RideRequest, double, double)>();

        foreach (var request in candidates)
        {
            var pickup = GeoDistance.Km(request.Origin, origin);
            var dropoff = GeoDistance.Km(request.Destination, destination);
            if (pickup > radiusKm || dropoff > radiusKm) continue;

            matches.Add((request, pickup, dropoff));
        }

        return matches;
    }

    /// <summary>
    /// Tier 2 — the request lies along the driver's route: both its ends fall in
    /// the corridor, in the driver's own direction of travel.
    ///
    /// The direction check is the half that matters. Both of a rider's ends can
    /// sit two hundred metres from the driver's line while the riders are
    /// travelling the other way, so the pickup's fraction along the route has to
    /// come before the drop-off's.
    /// </summary>
    private static List<(RideRequest Request, double Pickup, double Dropoff)> Corridor(
        List<RideRequest> candidates, LineString route, HashSet<int> exclude)
    {
        var corridorKm = MatchRules.CorridorMeters / 1000.0;
        var routeKm = RouteGeometry.LengthKm(route);
        var maxDetourKm = MatchRules.MaxDetourKm(routeKm);
        var matches = new List<(RideRequest, double, double)>();

        foreach (var request in candidates)
        {
            if (exclude.Contains(request.Id)) continue;

            var pickup = RouteGeometry.Locate(route, request.Origin);
            var dropoff = RouteGeometry.Locate(route, request.Destination);

            if (pickup.OffRouteKm > corridorKm || dropoff.OffRouteKm > corridorKm) continue;
            if (pickup.Fraction >= dropoff.Fraction) continue;
            if (pickup.OffRouteKm + dropoff.OffRouteKm > maxDetourKm) continue;

            matches.Add((request, pickup.OffRouteKm, dropoff.OffRouteKm));
        }

        return matches;
    }

    /// <summary>
    /// Both bands, scored and ordered.
    ///
    /// The same shape as the rider's ranking: a normalised detour-plus-wait
    /// score, plus a flat step per band that no score can climb, so a request
    /// going the driver's way can never be beaten by one that merely lies along
    /// it and the bands stay renderable as sections.
    /// </summary>
    private static List<DemandMatch> Rank(
        List<(RideRequest Request, double Pickup, double Dropoff)> direct,
        List<(RideRequest Request, double Pickup, double Dropoff)> corridor,
        DemandSearchInput input, int matchRadiusMeters)
    {
        var radiusKm = matchRadiusMeters / 1000.0;
        var scale = RankScale(radiusKm);

        var rows = direct.Select(m => Build(m, SearchTier.Direct))
            .Concat(corridor.Select(m => Build(m, SearchTier.OnTheWay)))
            .ToList();

        return input.SortBy switch
        {
            SearchSort.Departure => [.. rows.OrderBy(m => m.Tier).ThenBy(m => m.Request.DepartAt)],
            SearchSort.Seats => [.. rows.OrderBy(m => m.Tier)
                .ThenByDescending(m => m.Request.SeatsWanted).ThenBy(m => m.TotalDetourKm)],
            SearchSort.Pickup => [.. rows.OrderBy(m => m.Tier).ThenBy(m => m.PickupDetourKm)],
            _ => [.. rows.OrderBy(m => m.Tier).ThenBy(Score)],
        };

        DemandMatch Build((RideRequest Request, double Pickup, double Dropoff) m, SearchTier tier)
            => new()
            {
                Tier = tier,
                Request = new RideRequestRow { Id = m.Request.Id, DepartAt = m.Request.DepartAt },
                PickupDetourKm = Math.Round(m.Pickup, 2),
                DropoffDetourKm = Math.Round(m.Dropoff, 2),
                TotalDetourKm = Math.Round(m.Pickup + m.Dropoff, 2),
                MinutesFromWhen = (int)Math.Round((m.Request.DepartAt - input.When).TotalMinutes),
            };

        double Score(DemandMatch m) =>
            (scale <= 0 ? 0 : m.TotalDetourKm / scale)
            + Math.Abs(m.MinutesFromWhen) / MatchRules.RankTimeScale.TotalMinutes;
    }

    /// <summary>
    /// What a full match radius of diversion is worth against an hour off the
    /// wanted departure — the two halves of the score, made addable.
    /// </summary>
    private static double RankScale(double radiusKm) => radiusKm * 2;

    /// <summary>
    /// Fills the ranked page's rows with the facts a card needs — the route, the
    /// riders on it, its conditions and what the platform's rates make a seat
    /// worth.
    ///
    /// Done after ranking and capping so it reads the participants of twenty
    /// requests rather than two hundred.
    /// </summary>
    private async Task Fill(List<DemandMatch> matches)
    {
        if (matches.Count == 0) return;

        var ids = matches.Select(m => m.Request.Id).ToList();
        var requests = await requestRepository.Where(r => ids.Contains(r.Id)).ToListAsync();
        var members = await participantRepository
            .Where(p => ids.Contains(p.RideRequestId)
                        && p.Status == RideRequestParticipantStatus.Active)
            .ToListAsync();
        var riderIds = members.Select(p => p.RiderId).Distinct().ToList();
        var riders = await userRepository.Where(u => riderIds.Contains(u.Id)).ToListAsync();

        var settings = (await appConfigurationService.Get()).Data;
        var baseAmount = settings?.FareBaseAmount ?? FareRules.DefaultBaseAmount;
        var perKm = settings?.FarePerKm ?? FareRules.DefaultPerKm;

        foreach (var match in matches)
        {
            var request = requests.FirstOrDefault(r => r.Id == match.Request.Id);
            if (request == null) continue;

            var onIt = members.Where(p => p.RideRequestId == request.Id).ToList();
            var firstId = onIt.OrderBy(p => p.Id).FirstOrDefault()?.RiderId;
            var km = GeoDistance.Km(request.Origin, request.Destination);

            match.Request = new RideRequestRow(request, onIt,
                riders.FirstOrDefault(u => u.Id == firstId),
                callerId: 0, km, FareRules.PerSeat(km, baseAmount, perKm));
        }
    }

    private static bool IsSamePoint(GeoPoint a, GeoPoint b) =>
        Math.Abs(a.Lat - b.Lat) < 1e-6 && Math.Abs(a.Lng - b.Lng) < 1e-6;
}
