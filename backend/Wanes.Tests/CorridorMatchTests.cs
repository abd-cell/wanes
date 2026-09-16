using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Search;
using Wanes.Areas.Services.Search.Models;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// Trips that pass the rider's way.
///
/// The second search band matches against the driver's <em>route</em> rather
/// than their two endpoints, which is the only way to find the trip that goes
/// straight past you on its way somewhere else. Three things have to hold, and
/// each is one test below: both of the rider's ends inside the corridor, the
/// pickup before the drop-off <em>along the driver's direction of travel</em>,
/// and the two stops together not adding more to the run than it can bear.
///
/// The direction check is the one that cannot be skipped. A trip going the
/// other way runs beside the very same line, so distance alone would call it a
/// match and send a rider the wrong way up the motorway.
/// </summary>
public class CorridorMatchTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;

    // A long west-to-east leg. The rider's own hop sits in the middle of it,
    // a few hundred metres off the line — the classic "you pass my street".
    private const double WestLat = 31.95, WestLng = 35.60;
    private const double EastLat = 31.95, EastLng = 36.20;
    private const double PickupLat = 31.9520, PickupLng = 35.80;
    private const double DropoffLat = 31.9520, DropoffLng = 36.00;

    private static FakeUnitOfWork Scene(double originLng = WestLng, double destinationLng = EastLng)
    {
        var uow = new FakeUnitOfWork();
        var driver = Build.Driver(DriverId);
        uow.Store<User>().Add(driver);
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));

        var origin = GeoFactory.Point(WestLat, originLng);
        var destination = GeoFactory.Point(EastLat, destinationLng);

        var trip = Build.Trip(10, driverId: DriverId, vehicleId: 1, seatsTotal: 3);
        trip.Driver = driver;
        trip.Origin = origin;
        trip.Destination = destination;
        trip.Route = GeoFactory.Line(origin, destination);
        trip.DepartAt = DateTime.UtcNow.AddHours(1);
        uow.Store<Trip>().Add(trip);
        return uow;
    }

    private static async Task<SearchResult> Search(FakeUnitOfWork uow,
        double pickupLat = PickupLat, double pickupLng = PickupLng,
        double dropoffLat = DropoffLat, double dropoffLng = DropoffLng) =>
        (await Make.Search(uow, RiderId).Search(new SearchInput
        {
            Origin = new GeoPoint { Lat = pickupLat, Lng = pickupLng, Address = "A" },
            Destination = new GeoPoint { Lat = dropoffLat, Lng = dropoffLng, Address = "B" },
            When = DateTime.UtcNow.AddHours(1),
            Seats = 1,
        })).Data!;

    [Fact]
    public async Task A_trip_passing_the_rider_is_found_even_though_its_ends_are_elsewhere()
    {
        var uow = Scene();

        var match = Assert.Single((await Search(uow)).Matches);

        Assert.Equal(SearchTier.OnTheWay, match.Tier);
        Assert.Equal(10, match.Trip.Id);
    }

    [Fact]
    public async Task The_walk_is_measured_to_the_point_on_the_route_not_to_the_drivers_origin()
    {
        var uow = Scene();

        var match = Assert.Single((await Search(uow)).Matches);

        // The driver's own origin is tens of kilometres west. What the rider
        // actually walks is the few hundred metres to the road.
        Assert.True(match.PickupWalkKm < 1,
            $"walked {match.PickupWalkKm} km to a pickup that should be next door");
        Assert.NotNull(match.PickupLat);
        Assert.NotNull(match.DropoffLat);
    }

    [Fact]
    public async Task A_trip_going_the_other_way_is_not_on_the_riders_way()
    {
        // Same line, opposite direction: east to west. Distance alone cannot
        // tell it from a match — the ordering along the route can.
        var uow = Scene(originLng: EastLng, destinationLng: WestLng);

        Assert.Empty((await Search(uow)).Matches);
    }

    [Fact]
    public async Task A_rider_too_far_off_the_route_is_not_on_the_way()
    {
        var uow = Scene();

        // A few kilometres north of the line — past the corridor, however
        // conveniently the trip is heading the right way.
        Assert.Empty((await Search(uow, pickupLat: WestLat + 0.05)).Matches);
    }

    [Fact]
    public async Task A_direct_match_stays_ahead_of_a_trip_that_merely_passes()
    {
        var uow = Scene();

        // A second trip whose own ends are the rider's, leaving a good deal
        // later — so only the tier can be putting it first.
        var driver = Build.Driver(DriverId + 1);
        uow.Store<User>().Add(driver);
        uow.Store<Vehicle>().Add(Build.Vehicle(2, userId: DriverId + 1));

        var origin = GeoFactory.Point(PickupLat, PickupLng);
        var destination = GeoFactory.Point(DropoffLat, DropoffLng);
        var direct = Build.Trip(11, driverId: DriverId + 1, vehicleId: 2, seatsTotal: 3);
        direct.Driver = driver;
        direct.Origin = origin;
        direct.Destination = destination;
        direct.Route = GeoFactory.Line(origin, destination);
        direct.DepartAt = DateTime.UtcNow.AddHours(9);
        uow.Store<Trip>().Add(direct);

        var matches = (await Search(uow)).Matches;

        Assert.Equal([11, 10], matches.Select(m => m.Trip.Id).ToList());
        Assert.Equal(SearchTier.Direct, matches[0].Tier);
        Assert.Equal(SearchTier.OnTheWay, matches[1].Tier);
    }

    // ── The geometry behind it ───────────────────────────────────────────────

    [Fact]
    public void A_point_projects_to_its_place_along_the_route()
    {
        var route = GeoFactory.Line(GeoFactory.Point(31.95, 35.60), GeoFactory.Point(31.95, 36.20));

        var quarter = RouteGeometry.Locate(route, GeoFactory.Point(31.95, 35.75));
        var threeQuarters = RouteGeometry.Locate(route, GeoFactory.Point(31.95, 36.05));

        Assert.Equal(0.25, quarter.Fraction, 2);
        Assert.Equal(0.75, threeQuarters.Fraction, 2);
        Assert.True(quarter.OffRouteKm < 0.01);
    }

    [Fact]
    public void A_point_beside_the_route_reports_how_far_off_it_is()
    {
        var route = GeoFactory.Line(GeoFactory.Point(31.95, 35.60), GeoFactory.Point(31.95, 36.20));

        var (_, offRouteKm) = RouteGeometry.Locate(route, GeoFactory.Point(31.96, 35.90));

        // A hundredth of a degree of latitude is a bit over a kilometre.
        Assert.InRange(offRouteKm, 1.0, 1.3);
    }

    [Fact]
    public void A_route_with_no_length_answers_honestly_rather_than_dividing_by_zero()
    {
        var point = GeoFactory.Point(31.95, 35.60);
        var route = GeoFactory.Line(point, point);

        var (fraction, offRouteKm) = RouteGeometry.Locate(route, GeoFactory.Point(31.95, 35.70));

        Assert.Equal(0, fraction);
        Assert.True(offRouteKm > 0);
    }

    [Fact]
    public void The_route_is_measured_in_kilometres_not_degrees()
    {
        var route = GeoFactory.Line(GeoFactory.Point(31.95, 35.60), GeoFactory.Point(31.95, 36.20));

        var km = RouteGeometry.LengthKm(route);

        Assert.InRange(km, 50, 60);
    }

    [Fact]
    public void The_detour_allowance_grows_with_the_run_but_never_vanishes()
    {
        // A share of the run, because the same 4 km is trivial on a motorway and
        // absurd across town — with a floor, or a short hop would refuse
        // everybody.
        Assert.Equal(MatchRules.MinDetourMeters / 1000.0, MatchRules.MaxDetourKm(2));
        Assert.Equal(25, MatchRules.MaxDetourKm(100));
    }
}
