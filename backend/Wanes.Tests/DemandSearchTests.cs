using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Search;
using Wanes.Areas.Services.Users.Availability;
using Wanes.Areas.Services.Search.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// A driver searching for riders.
///
/// The mirror of the rider's search, and it has to earn the same promise: a
/// driver only ever sees a trip they could actually take. Everything the board
/// refuses — an unverified driver, no car, a pool too big for it, a clash in the
/// diary, a trip they are riding on themselves — has to be absent here too,
/// because a list that offers what the API then refuses is worse than a short
/// one.
///
/// The two bands are the same idea as the rider's, pointed the other way. A
/// direct match is a pool going where the driver is going; an on-the-way match
/// is a pool whose two ends fall along the driver's route, in the driver's own
/// direction of travel. The direction half is the one worth testing: two points
/// can sit beside a line while the people at them are travelling the other way.
/// </summary>
public class DemandSearchTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;

    // Amman → Irbid, roughly: the driver's journey in every test here.
    private static readonly GeoPoint Amman = new() { Lat = 31.95, Lng = 35.92, Address = "Amman" };
    private static readonly GeoPoint Irbid = new() { Lat = 32.55, Lng = 35.85, Address = "Irbid" };

    private static readonly DateTime When = DateTime.UtcNow.AddHours(4);

    private static FakeUnitOfWork Scene(int capacity = 4,
        DriverStatus status = DriverStatus.Verified, Gender driverGender = Gender.Male)
    {
        var uow = new FakeUnitOfWork();
        var driver = Build.Driver(DriverId, status);
        driver.Gender = driverGender;
        uow.Store<User>().Add(driver);
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId, capacity: capacity));
        uow.Store<User>().Add(Build.Rider(RiderId));
        return uow;
    }

    /// <summary>Demand between two given points, with its rider on it.</summary>
    private static RideRequest Posting(FakeUnitOfWork uow, int id, GeoPoint from, GeoPoint to,
        int seats = 1, int riderId = RiderId, DateTime? departAt = null)
    {
        var origin = GeoFactory.Point(from.Lat, from.Lng);
        var destination = GeoFactory.Point(to.Lat, to.Lng);
        var request = new RideRequest
        {
            Id = id,
            OriginAddress = from.Address, Origin = origin,
            DestinationAddress = to.Address, Destination = destination,
            Route = GeoFactory.Line(origin, destination),
            DepartAt = departAt ?? When,
            TimeWindowMinutes = 30,
            SeatsRequested = seats,
            RadiusMeters = 5000,
            Status = RideRequestStatus.Open,
        };
        uow.Store<RideRequest>().Add(request);
        uow.Store<RideRequestParticipant>().Add(new RideRequestParticipant
        {
            Id = id * 100, RideRequestId = id, RiderId = riderId, Seats = seats,
            Status = RideRequestParticipantStatus.Active,
        });
        return request;
    }

    private static DemandSearchService Service(FakeUnitOfWork uow, int driverId = DriverId) =>
        new(new FakeSecurityManager(driverId), new FakeAuditService(),
            new FakeAppConfigurationService(),
            new DriverAvailabilityService(uow.Repository<Trip>(), new FakeSecurityManager(driverId)),
            uow.Repository<RideRequest>(), uow.Repository<RideRequestParticipant>(),
            uow.Repository<User>(), uow.Repository<Vehicle>());

    private static async Task<DemandSearchResult> Search(FakeUnitOfWork uow,
        int minSeats = 0, GenderPolicy carrying = GenderPolicy.Any, int driverId = DriverId) =>
        (await Service(uow, driverId).Search(new DemandSearchInput
        {
            Origin = Amman,
            Destination = Irbid,
            When = When,
            MinSeats = minSeats,
            RiderGenderPolicy = carrying,
        })).Data!;

    // ── The two bands ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_pool_going_where_the_driver_is_going_is_a_direct_match()
    {
        var uow = Scene();
        Posting(uow, 30, Amman, Irbid);

        var match = Assert.Single((await Search(uow)).Matches);

        Assert.Equal(SearchTier.Direct, match.Tier);
        Assert.Equal(30, match.Request.Id);
    }

    [Fact]
    public async Task A_pool_along_the_way_is_the_second_band()
    {
        // Both ends sit on the Amman → Irbid line, in that order, but neither is
        // near the driver's own ends: a diversion, not the same journey.
        var uow = Scene();
        Posting(uow, 31,
            new GeoPoint { Lat = 32.10, Lng = 35.90, Address = "Jerash road" },
            new GeoPoint { Lat = 32.40, Lng = 35.87, Address = "Near Irbid" });

        var match = Assert.Single((await Search(uow)).Matches);

        Assert.Equal(SearchTier.OnTheWay, match.Tier);
        Assert.True(match.TotalDetourKm >= 0);
    }

    [Fact]
    public async Task A_pool_travelling_the_other_way_is_not_on_the_driver_s_way()
    {
        // The same two points as the on-the-way case, reversed. Distance to the
        // line is identical; the direction is not, and that is the whole test.
        var uow = Scene();
        Posting(uow, 32,
            new GeoPoint { Lat = 32.40, Lng = 35.87, Address = "Near Irbid" },
            new GeoPoint { Lat = 32.10, Lng = 35.90, Address = "Jerash road" });

        Assert.Empty((await Search(uow)).Matches);
    }

    [Fact]
    public async Task A_direct_match_outranks_one_that_is_merely_on_the_way()
    {
        var uow = Scene();
        Posting(uow, 31,
            new GeoPoint { Lat = 32.10, Lng = 35.90, Address = "Jerash road" },
            new GeoPoint { Lat = 32.40, Lng = 35.87, Address = "Near Irbid" });
        Posting(uow, 30, Amman, Irbid);

        var matches = (await Search(uow)).Matches;

        Assert.Equal(2, matches.Count);
        Assert.Equal(SearchTier.Direct, matches[0].Tier);
        Assert.Equal(30, matches[0].Request.Id);
    }

    // ── What the driver could not take, they are not shown ───────────────────

    [Fact]
    public async Task A_pool_bigger_than_the_car_is_not_offered()
    {
        var uow = Scene(capacity: 2);
        Posting(uow, 30, Amman, Irbid, seats: 3);

        Assert.Empty((await Search(uow)).Matches);
    }

    [Fact]
    public async Task A_driver_the_riders_ruled_out_sees_nothing()
    {
        var uow = Scene(driverGender: Gender.Male);
        Posting(uow, 30, Amman, Irbid).DriverGenderPolicy = GenderPolicy.FemaleOnly;

        Assert.Empty((await Search(uow)).Matches);
    }

    [Fact]
    public async Task A_driver_out_on_the_road_sees_nothing_at_all()
    {
        var uow = Scene();
        Posting(uow, 30, Amman, Irbid);
        uow.Store<Trip>().Add(Build.Trip(10, DriverId, 1, status: TripStatus.Active));

        Assert.Empty((await Search(uow)).Matches);
    }

    [Fact]
    public async Task A_departure_that_clashes_with_one_of_their_own_is_dropped()
    {
        var uow = Scene();
        Posting(uow, 30, Amman, Irbid);

        // A trip they are already committed to at the same hour. Not "engaged" —
        // just promised — so the clash is per trip, not a blanket refusal.
        var promised = Build.Trip(10, DriverId, 1, status: TripStatus.Posted);
        promised.DepartAt = When;
        uow.Store<Trip>().Add(promised);

        Assert.Empty((await Search(uow)).Matches);
    }

    [Fact]
    public async Task Nobody_is_offered_a_trip_they_are_riding_on_themselves()
    {
        var uow = Scene();
        Posting(uow, 30, Amman, Irbid, riderId: DriverId);

        Assert.Empty((await Search(uow)).Matches);
    }

    [Fact]
    public async Task An_unverified_driver_is_refused_rather_than_shown_an_empty_list()
    {
        // A different answer from "nothing matched": their application is the
        // thing to fix, and a blank list would send them looking for a route.
        var uow = Scene(status: DriverStatus.Pending);
        Posting(uow, 30, Amman, Irbid);

        var res = await Service(uow).Search(new DemandSearchInput
        {
            Origin = Amman, Destination = Irbid, When = When,
        });

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.DriverNotVerified, res.ErrorCode);
    }

    [Fact]
    public async Task A_driver_with_no_car_is_told_so()
    {
        var uow = Scene();
        uow.Store<Vehicle>().Clear();
        Posting(uow, 30, Amman, Irbid);

        var res = await Service(uow).Search(new DemandSearchInput
        {
            Origin = Amman, Destination = Irbid, When = When,
        });

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.VehicleNotFound, res.ErrorCode);
    }

    // ── The driver's own filters ─────────────────────────────────────────────

    [Fact]
    public async Task A_minimum_seat_count_hides_the_pools_below_it()
    {
        var uow = Scene();
        Posting(uow, 30, Amman, Irbid, seats: 1);
        Posting(uow, 31, Amman, Irbid, seats: 3, riderId: 6);
        uow.Store<User>().Add(Build.Rider(6));

        var matches = await Search(uow, minSeats: 2);

        Assert.Equal(31, Assert.Single(matches.Matches).Request.Id);
    }

    [Fact]
    public async Task Who_the_driver_will_carry_filters_the_list_and_nothing_else()
    {
        // Stated per search, like the rider's condition on their driver: it
        // shapes what comes back and is not written onto anything.
        var uow = Scene();
        var man = Build.Rider(6);
        man.Gender = Gender.Male;
        uow.Store<User>().Add(man);
        uow.Store<User>().First(u => u.Id == RiderId).Gender = Gender.Female;

        Posting(uow, 30, Amman, Irbid);                    // a woman's pool
        Posting(uow, 31, Amman, Irbid, riderId: 6);        // a man's pool

        var matches = await Search(uow, carrying: GenderPolicy.FemaleOnly);

        Assert.Equal(30, Assert.Single(matches.Matches).Request.Id);
    }

    [Fact]
    public async Task The_seats_on_offer_come_from_the_car_when_the_driver_says_nothing()
    {
        var uow = Scene(capacity: 6);
        Posting(uow, 30, Amman, Irbid);

        Assert.Equal(6, (await Search(uow)).SeatsOffered);
    }

    // ── What a card carries ──────────────────────────────────────────────────

    [Fact]
    public async Task A_card_carries_the_riders_the_route_and_a_suggested_price()
    {
        var uow = Scene();
        Posting(uow, 30, Amman, Irbid, seats: 2);

        var match = Assert.Single((await Search(uow)).Matches);

        Assert.Equal("Amman", match.Request.OriginAddress);
        Assert.Equal("Irbid", match.Request.DestinationAddress);
        Assert.Equal(2, match.Request.SeatsWanted);
        Assert.Equal(1, match.Request.RiderCount);
        Assert.True(match.Request.SuggestedPricePerSeat > 0);
        Assert.True(match.Request.DistanceKm > 0);
    }

    [Fact]
    public async Task A_card_says_how_far_off_the_wanted_hour_the_pool_is()
    {
        var uow = Scene();
        Posting(uow, 30, Amman, Irbid, departAt: When.AddMinutes(20));

        var match = Assert.Single((await Search(uow)).Matches);

        Assert.Equal(20, match.MinutesFromWhen);
    }
}
