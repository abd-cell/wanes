using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.RideRequests.Models;
using Wanes.Areas.Services.Search.Models;
using Wanes.Areas.Services.Trips.Models;
using Wanes.DataAccess.Repositories;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Search;

/// <summary>
/// The rider's home search: what can I take, from here to there, around then.
///
/// Three bands of answer and one fallback, in one response
/// (<see cref="SearchResult"/>). Every band is filtered by the same eligibility
/// rules that booking refuses on, in both directions — a list that offers what
/// the API then refuses is worse than a short list.
/// </summary>
public class SearchService : ISearchService
{
    /// <summary>How many bookable trips one page shows, across both bands.</summary>
    private const int MaxResults = 20;

    /// <summary>How many joinable postings to offer. A short list: this is a suggestion, not a board.</summary>
    private const int MaxRequests = 5;

    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly IAppConfigurationService appConfigurationService;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<Booking> bookingRepository;
    private readonly IRepository<RideRequest> requestRepository;
    private readonly IRepository<RideRequestParticipant> participantRepository;

    public SearchService(
        ISecurityManager securityManager,
        IAuditService auditService,
        IAppConfigurationService appConfigurationService,
        IRepository<Trip> tripRepository,
        IRepository<User> userRepository,
        IRepository<Booking> bookingRepository,
        IRepository<RideRequest> requestRepository,
        IRepository<RideRequestParticipant> participantRepository)
    {
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.appConfigurationService = appConfigurationService;
        this.tripRepository = tripRepository;
        this.userRepository = userRepository;
        this.bookingRepository = bookingRepository;
        this.requestRepository = requestRepository;
        this.participantRepository = participantRepository;
    }

    public async Task<BaseResponse<SearchResult>> Search(SearchInput input)
    {
        var riderId = securityManager.RequireUserId();
        var rider = await userRepository.GetByIdAsync(riderId);
        if (rider == null) return new BaseResponse<SearchResult>(default, ErrorCode.NotFound);

        if (IsSamePoint(input.Origin, input.Destination))
            return new BaseResponse<SearchResult>(default, ErrorCode.OriginEqualsDestination);
        if (input.Seats < 1) input.Seats = 1;

        var origin = GeoFactory.Point(input.Origin.Lat, input.Origin.Lng);
        var destination = GeoFactory.Point(input.Destination.Lat, input.Destination.Lng);
        var matchRadius = MatchRules.RadiusFor(input.Nearby);
        var now = DateTime.UtcNow;

        var candidates = await Bookable(rider, input, origin, destination, now);

        // Tier 1 first, and its ids are what keeps a trip out of tier 2: a trip
        // whose own ends are near the rider's is a direct match, and offering it
        // again as "on the way" would be the same row twice.
        var direct = await Direct(candidates, origin, destination, matchRadius);
        var directIds = direct.Select(t => t.Id).ToHashSet();
        var corridor = await Corridor(candidates, origin, destination, directIds);

        var matches = Rank(direct, corridor, origin, destination, input, matchRadius);

        var settings = (await appConfigurationService.Get()).Data;
        var km = GeoDistance.Km(origin, destination);
        var speed = settings?.AverageSpeedKmh ?? RiderTripRules.DefaultAverageSpeedKmh;

        await auditService.LogAsync(AuditActions.SearchCarpool, nameof(Trip));

        return new BaseResponse<SearchResult>(new SearchResult
        {
            Matches = matches,
            Requests = await Joinable(rider, input, origin, destination, matchRadius, now, settings),
            EarliestDepartAt = RiderTripRules.EarliestDeparture(now, km, input.Seats, speed),
        });
    }

    /// <summary>
    /// Trips this rider could actually take, before either band's geometry is
    /// considered: seats, timing, the driver's conditions on them, and their own
    /// condition on the driver.
    ///
    /// One query for both tiers. They differ only in *where* the trip has to be,
    /// and running the same seven predicates twice would be the obvious place
    /// for the two to drift apart.
    /// </summary>
    private async Task<List<Trip>> Bookable(User rider, SearchInput input, Point origin,
        Point destination, DateTime now)
    {
        var boardingFloor = now - MatchRules.BoardingGrace;
        var liveFixFloor = MatchRules.LiveFixFloor(now);
        var matchRadius = MatchRules.RadiusFor(input.Nearby);

        // A seat this rider already holds is not a seat they can take again —
        // booking it would only answer AlreadyBooked, so keep those trips out of
        // the results instead of showing an offer that cannot be accepted.
        var bookedTripIds = await bookingRepository
            .Where(b => b.RiderId == rider.Id && b.Status != BookingStatus.Cancelled)
            .Select(b => b.TripId)
            .ToListAsync();

        // The eligibility predicates, in the form SQL can answer. The rider's own
        // side is known here, so each one collapses to a comparison against a
        // constant rather than a translated call into RiderEligibilityRules —
        // which cannot be translated at all. Booking re-checks with the real
        // rules; these only decide what is worth showing.
        var riderPolicy = RiderEligibilityRules.PolicyFor(rider.Gender);
        var riderAge = RiderEligibilityRules.AgeAt(rider.DateOfBirth, input.When);
        var wantedDriver = input.DriverGenderPolicy;
        var wantedDriverGender = wantedDriver == GenderPolicy.MaleOnly ? Gender.Male : Gender.Female;

        return await tripRepository
            .Where(t => t.Status == TripStatus.Posted
                        && t.SeatsLeft >= input.Seats
                        && t.DriverId != rider.Id
                        && !bookedTripIds.Contains(t.Id)
                        // A disabled account is not driving anyone anywhere.
                        && (t.Driver == null || !t.Driver.IsDisabled)
                        // The floor is a short grace rather than "now": a Posted
                        // trip a few minutes past its departure has not left, it
                        // is boarding — the driver is late, or is still on their
                        // way to a first pickup. What it excludes is the trip
                        // from this morning that was never started at all.
                        && t.DepartAt > boardingFloor
                        // No window on the wanted departure. A trip with free
                        // seats that has not gone is discoverable whenever it
                        // leaves; how close it is to the hour the rider asked
                        // for ranks it instead. As a filter this hid a usable
                        // trip two hours out and left the rider with nothing.
                        //
                        // The driver's conditions on this rider:
                        && (t.GenderPolicy == GenderPolicy.Any || t.GenderPolicy == riderPolicy)
                        && (t.MinAge == null || (riderAge != null && riderAge >= t.MinAge))
                        && (t.MaxAge == null || (riderAge != null && riderAge <= t.MaxAge))
                        // and this rider's condition on the driver:
                        && (wantedDriver == GenderPolicy.Any
                            || (t.Driver != null && t.Driver.Gender == wantedDriverGender))
                        // And, when we actually know where the driver is, they
                        // have to be around here too. A trip whose pickup point
                        // is next door but whose driver is currently an hour
                        // away is a notional match, not a real one.
                        //
                        // A missing or stale fix is ignored rather than
                        // disqualifying: a driver only reports while the app is
                        // running, so requiring one would hide every trip posted
                        // for a future day.
                        && (t.Driver == null
                            || t.Driver.LastLocation == null
                            || t.Driver.LastLocationAt == null
                            || t.Driver.LastLocationAt < liveFixFloor
                            || t.Driver.LastLocation.IsWithinDistance(origin, matchRadius)),
                query => query.Include(t => t.Driver).Include(t => t.Vehicle))
            .OrderBy(t => t.Origin.Distance(origin) + t.Destination.Distance(destination))
            .Take(MatchRules.CandidatePool)
            .ToListAsync();
    }

    /// <summary>
    /// Tier 1 — both of the trip's own ends inside the radius of the rider's.
    ///
    /// Also the direction check, for free: a trip going the other way has its
    /// origin near the rider's destination, so it cannot pass both halves.
    /// </summary>
    private async Task<List<Trip>> Direct(List<Trip> candidates, Point origin, Point destination,
        int matchRadiusMeters)
    {
        await Task.CompletedTask;
        var radiusKm = matchRadiusMeters / 1000.0;

        return candidates
            .Where(t => GeoDistance.Km(t.Origin, origin) <= radiusKm
                        && GeoDistance.Km(t.Destination, destination) <= radiusKm)
            .ToList();
    }

    /// <summary>
    /// Tier 2 — the trip's route passes the rider.
    ///
    /// Three conditions, and all three are needed. Both of the rider's ends have
    /// to be inside the corridor around the route; the pickup has to come
    /// *before* the drop-off along the driver's direction of travel, which is
    /// what distance alone cannot tell (a trip going the other way runs beside
    /// the same line); and the two stops together must not add more to the run
    /// than <see cref="MatchRules.MaxDetourKm"/> allows, or a rider 4 km off the
    /// middle of a motorway counts as being on the way.
    /// </summary>
    private async Task<List<(Trip Trip, Coordinate Pickup, Coordinate Dropoff)>> Corridor(
        List<Trip> candidates, Point origin, Point destination, HashSet<int> exclude)
    {
        await Task.CompletedTask;
        var corridorKm = MatchRules.CorridorMeters / 1000.0;
        var matches = new List<(Trip, Coordinate, Coordinate)>();

        foreach (var trip in candidates)
        {
            if (exclude.Contains(trip.Id) || trip.Route == null) continue;

            var pickup = RouteGeometry.Locate(trip.Route, origin);
            var dropoff = RouteGeometry.Locate(trip.Route, destination);

            if (pickup.OffRouteKm > corridorKm || dropoff.OffRouteKm > corridorKm) continue;
            if (pickup.Fraction >= dropoff.Fraction) continue;

            var routeKm = RouteGeometry.LengthKm(trip.Route);
            if (pickup.OffRouteKm + dropoff.OffRouteKm > MatchRules.MaxDetourKm(routeKm)) continue;

            matches.Add((
                trip,
                Nearest(trip.Route, pickup.Fraction),
                Nearest(trip.Route, dropoff.Fraction)));
        }

        return matches;
    }

    /// <summary>
    /// Both bands, scored and ordered.
    ///
    /// The score is the old walk-plus-wait — normalised so the two can be added,
    /// a full match radius of walking costing the same as an hour off the wanted
    /// hour — plus a flat step per tier. The step is larger than the score can
    /// ever reach, so a direct match cannot be beaten by a corridor one however
    /// convenient the corridor one looks, and the bands stay renderable as
    /// sections.
    ///
    /// Distances go through <see cref="GeoDistance"/> and not
    /// <c>Point.Distance</c>: these entities are in memory now, where NTS
    /// answers in degrees.
    /// </summary>
    private List<SearchMatch> Rank(
        List<Trip> direct,
        List<(Trip Trip, Coordinate Pickup, Coordinate Dropoff)> corridor,
        Point origin, Point destination, SearchInput input, int matchRadiusMeters)
    {
        var radiusKm = matchRadiusMeters / 1000.0;
        var timeScale = MatchRules.RankTimeScale.TotalMinutes;

        var scored = new List<(SearchMatch Match, double Score)>();

        foreach (var trip in direct)
        {
            var pickupKm = GeoDistance.Km(trip.Origin, origin);
            var dropoffKm = GeoDistance.Km(trip.Destination, destination);
            scored.Add((
                new SearchMatch
                {
                    Tier = SearchTier.Direct,
                    Trip = new TripOutput(trip, trip.Driver),
                    PickupWalkKm = Round(pickupKm),
                    DropoffWalkKm = Round(dropoffKm),
                },
                Score(pickupKm + dropoffKm, trip.DepartAt, input.When, radiusKm, timeScale)));
        }

        foreach (var (trip, pickup, dropoff) in corridor)
        {
            // Measured to the points on the route the driver will pass, not to
            // their own endpoints. On a corridor match those are the only
            // distances the rider actually walks.
            var pickupKm = GeoDistance.Km(origin.Y, origin.X, pickup.Y, pickup.X);
            var dropoffKm = GeoDistance.Km(destination.Y, destination.X, dropoff.Y, dropoff.X);
            scored.Add((
                new SearchMatch
                {
                    Tier = SearchTier.OnTheWay,
                    Trip = new TripOutput(trip, trip.Driver),
                    PickupWalkKm = Round(pickupKm),
                    DropoffWalkKm = Round(dropoffKm),
                    PickupLat = pickup.Y,
                    PickupLng = pickup.X,
                    DropoffLat = dropoff.Y,
                    DropoffLng = dropoff.X,
                },
                Score(pickupKm + dropoffKm, trip.DepartAt, input.When, radiusKm, timeScale)));
        }

        return scored
            // The band is its own key rather than a penalty folded into one
            // number. As arithmetic the rule only held while every sort key
            // stayed smaller than the penalty, and two of them did not: a
            // departure counted in minutes-since-year-one dwarfs it outright,
            // and an unpriced trip was pushed past every finite value there is.
            // Both quietly put a passing trip above one going the rider's way.
            .OrderBy(x => x.Match.Tier)
            .ThenBy(x => SortKey(x, input.SortBy))
            // Same tie-break for every sort, so two identical searches cannot
            // disagree about the order of equally good matches: the shorter
            // walk, then the earlier departure.
            .ThenBy(x => x.Match.PickupWalkKm + x.Match.DropoffWalkKm)
            .ThenBy(x => x.Match.Trip.DepartAt)
            .Take(MaxResults)
            .Select(x => x.Match)
            .ToList();
    }

    /// <summary>
    /// The rider's chosen order <em>within</em> a band. Which band a match sits
    /// in is decided before this ever runs, so a key here can be any magnitude
    /// it likes and "cheapest" still means the cheapest trip going the rider's
    /// way rather than the cheapest one that happens to pass nearby.
    ///
    /// Ranking moved in-memory when the corridor arrived — a projected walk
    /// cannot be computed in SQL — so the sorts moved with it. Lower comes
    /// first, hence the negations on the two where "more" is better.
    /// </summary>
    private static double SortKey((SearchMatch Match, double Score) scored, SearchSort sort)
    {
        var trip = scored.Match.Trip;

        return sort switch
        {
            SearchSort.Departure => (trip.DepartAt - DateTime.UnixEpoch).TotalMinutes,

            // An unpriced trip sorts last on "cheapest": a rider asking for the
            // cheapest wants a number, and null is not a low one.
            SearchSort.Price => trip.PricePerSeat is { } price ? (double)price : double.MaxValue,

            SearchSort.Rating => -trip.DriverRating,
            SearchSort.Pickup => scored.Match.PickupWalkKm,
            SearchSort.Seats => -trip.SeatsLeft,
            _ => scored.Score,
        };
    }

    /// <summary>
    /// Walk plus wait, weighed against each other — the default order inside a
    /// band. Carries no tier term: the band is a separate key now.
    /// </summary>
    private static double Score(double walkKm, DateTime departAt, DateTime when,
        double radiusKm, double timeScale)
    {
        var waitMinutes = Math.Abs((departAt - when).TotalMinutes);
        return walkKm / radiusKm + waitMinutes / timeScale;
    }

    /// <summary>
    /// Tier 3 — open ride requests along this route that the rider could join
    /// instead of creating a near-identical one.
    ///
    /// Checked both ways, as joining is: the rider against the pool's
    /// conditions, and — because a joiner brings their own — the pool's riders
    /// against the rider's stated preferences. The service refuses the rest at
    /// the tap; this only decides what is worth suggesting, and suggesting
    /// something that would be refused is worse than suggesting nothing.
    /// </summary>
    private async Task<List<RideRequestRow>> Joinable(User rider, SearchInput input, Point origin,
        Point destination, int matchRadius, DateTime now,
        Configuration.Models.AppConfigurationOutput? settings)
    {
        var riderPolicy = RiderEligibilityRules.PolicyFor(rider.Gender);
        var wantedDriver = input.DriverGenderPolicy;

        // A request the rider is already on is not a suggestion. Read off their
        // own participations, which is also what excludes the ones they wrote.
        var onIt = await participantRepository
            .Where(p => p.RiderId == rider.Id
                        && p.Status == RideRequestParticipantStatus.Active)
            .Select(p => p.RideRequestId)
            .ToListAsync();

        var requests = await requestRepository
            .Where(r => r.Status == RideRequestStatus.Open
                        && r.DepartAt > now
                        && !onIt.Contains(r.Id)
                        && r.SeatsRequested + input.Seats <= RiderTripRules.MaxSeats
                        // The pool's condition on this rider.
                        && (r.GenderPolicy == GenderPolicy.Any
                            || r.GenderPolicy == riderPolicy)
                        // Two opposed driver policies leave nobody who could
                        // drive, which is a refusal at the tap — do not suggest it.
                        && (r.DriverGenderPolicy == GenderPolicy.Any
                            || wantedDriver == GenderPolicy.Any
                            || r.DriverGenderPolicy == wantedDriver)
                        && r.Origin.IsWithinDistance(origin, matchRadius)
                        && r.Destination.IsWithinDistance(destination, matchRadius))
            .OrderBy(r => r.Origin.Distance(origin) + r.Destination.Distance(destination))
            .Take(MaxRequests)
            .ToListAsync();
        if (requests.Count == 0) return [];

        // The age half of the pool's conditions, and the whole of the riders'
        // conditions on the newcomer, need the real rules — which do not
        // translate to SQL. The set is at most five rows by here.
        var offerable = requests
            .Where(r => RiderEligibilityRules.CanRide(rider, r.Conditions, r.DepartAt))
            .ToList();
        if (offerable.Count == 0) return [];

        // The participants on those requests, in one read, so each card can say
        // how many riders are already on it and who wrote it.
        var ids = offerable.Select(r => r.Id).ToList();
        var participants = await participantRepository
            .Where(p => ids.Contains(p.RideRequestId)
                        && p.Status == RideRequestParticipantStatus.Active)
            .ToListAsync();
        var authorIds = participants.Select(p => p.RiderId).Distinct().ToList();
        var authors = await userRepository.Where(u => authorIds.Contains(u.Id)).ToListAsync();

        var baseAmount = settings?.FareBaseAmount ?? FareRules.DefaultBaseAmount;
        var perKm = settings?.FarePerKm ?? FareRules.DefaultPerKm;

        return offerable
            .Select(r =>
            {
                var km = GeoDistance.Km(r.Origin, r.Destination);
                var members = participants.Where(p => p.RideRequestId == r.Id).ToList();
                var firstId = members.OrderBy(p => p.Id).FirstOrDefault()?.RiderId;
                return new RideRequestRow(r, members, authors.FirstOrDefault(u => u.Id == firstId),
                    rider.Id, km, FareRules.PerSeat(km, baseAmount, perKm));
            })
            .ToList();
    }

    /// <summary>The point on the route at a given fraction along it.</summary>
    private static Coordinate Nearest(NetTopologySuite.Geometries.LineString route, double fraction)
    {
        var indexed = new NetTopologySuite.LinearReferencing.LengthIndexedLine(route);
        return indexed.ExtractPoint(fraction * route.Length);
    }

    private static double Round(double km) => Math.Round(km, 2);

    private static bool IsSamePoint(GeoPoint a, GeoPoint b) =>
        Math.Abs(a.Lat - b.Lat) < 1e-6 && Math.Abs(a.Lng - b.Lng) < 1e-6;
}
