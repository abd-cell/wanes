using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Search;
using Wanes.Areas.Services.Search.Models;
using Wanes.Areas.Services.Trips;
using Wanes.Areas.Services.Users.Availability;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// When a trip can be found, and when it stops being findable.
///
/// The rule is the spec's: a trip is discoverable while it has seats and has not
/// departed. Two things used to break it.
///
/// A ±30 minute window on the wanted departure was a *filter*, so a trip with
/// free seats leaving two hours later was not ranked lower — it was invisible,
/// and the rider fell through to a hail with a perfectly good match sitting
/// unshown. The wanted hour now only ranks.
///
/// And there was no state for "the driver has set off", so a trip stayed
/// searchable all the way to the first kerb and dropped out on arrival — exactly
/// backwards. <see cref="TripStatus.EnRoute"/> is where it leaves search now.
/// </summary>
public class DiscoverabilityTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;

    // Both ends within the Nearby radius of the rider's own, so distance is
    // never what decides these tests.
    private const double OriginLat = 31.9539;
    private const double OriginLng = 35.9106;
    private const double DestLat = 32.0100;
    private const double DestLng = 35.8700;

    private static SearchService Search(FakeUnitOfWork uow) =>
        new(uow, new FakeSecurityManager(RiderId), new FakeAuditService(),
            new FakeNotificationService(), new FakeAppConfigurationService(),
            uow.Repository<Trip>(), uow.Repository<User>(), uow.Repository<Vehicle>(),
            uow.Repository<RideRequest>(), uow.Repository<Booking>());

    private static TripService Trips(FakeUnitOfWork uow) =>
        new(uow, new FakeSecurityManager(DriverId), new FakeAuditService(),
            new FakeNotificationService(),
            new DriverAvailabilityService(uow.Repository<Trip>()),
            uow.Repository<Trip>(), uow.Repository<TripStatusHistory>(), uow.Repository<Vehicle>(),
            uow.Repository<User>(), uow.Repository<Booking>(), uow.Repository<RideRequest>());

    private static SearchInput Wanted(DateTime when) => new()
    {
        Origin = new GeoPoint { Lat = OriginLat, Lng = OriginLng, Address = "A" },
        Destination = new GeoPoint { Lat = DestLat, Lng = DestLng, Address = "B" },
        When = when,
        Seats = 1,
    };

    private static FakeUnitOfWork Scene()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(DriverId));
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));
        return uow;
    }

    private static Trip AddTrip(FakeUnitOfWork uow, int id, DateTime departAt,
        int seatsLeft = 3, TripStatus status = TripStatus.Posted)
    {
        var trip = Build.Trip(id, driverId: DriverId, vehicleId: 1, seatsTotal: 3, status: status);
        trip.Origin = GeoFactory.Point(OriginLat, OriginLng);
        trip.Destination = GeoFactory.Point(DestLat, DestLng);
        trip.DepartAt = departAt;
        trip.SeatsLeft = seatsLeft;
        // The fake repositories ignore Include shapers, so the navigation the
        // service reads (to take the driver offline on setting out) is wired here.
        trip.Driver = uow.Store<User>().FirstOrDefault(u => u.Id == DriverId);
        uow.Store<Trip>().Add(trip);
        return trip;
    }

    // ── The wanted hour ranks, it does not exclude ───────────────────────────

    [Fact]
    public async Task A_trip_well_outside_the_old_window_is_still_found()
    {
        // The regression this file exists for. Six hours is twelve times the old
        // ±30 minute window, and the trip has seats and has not departed, so the
        // spec says it is discoverable.
        var uow = Scene();
        var now = DateTime.UtcNow;
        AddTrip(uow, 10, now.AddHours(6));

        var res = await Search(uow).Search(Wanted(now));

        Assert.Equal(SearchMode.Carpool, res.Data!.Mode);
        Assert.Equal(10, Assert.Single(res.Data.Matches).Id);
    }

    [Fact]
    public async Task A_trip_days_away_is_still_found()
    {
        var uow = Scene();
        var now = DateTime.UtcNow;
        AddTrip(uow, 10, now.AddDays(4));

        var res = await Search(uow).Search(Wanted(now));

        Assert.Equal(SearchMode.Carpool, res.Data!.Mode);
        Assert.Single(res.Data.Matches);
    }

    [Fact]
    public async Task The_hail_only_opens_when_there_is_genuinely_nothing()
    {
        // The consequence of ranking rather than filtering, and the point of it:
        // a request is a last resort, not what a rider gets for asking about an
        // hour no driver happened to pick.
        var uow = Scene();
        var res = await Search(uow).Search(Wanted(DateTime.UtcNow));

        Assert.Equal(SearchMode.Hail, res.Data!.Mode);
    }

    [Fact]
    public async Task The_trip_closest_to_the_wanted_hour_ranks_first()
    {
        // Everything else equal, waiting less wins — otherwise "discoverable
        // whenever it leaves" would just bury the useful match.
        var uow = Scene();
        var now = DateTime.UtcNow;
        AddTrip(uow, 10, now.AddDays(2));
        AddTrip(uow, 11, now.AddMinutes(20));
        AddTrip(uow, 12, now.AddHours(9));

        var res = await Search(uow).Search(Wanted(now));

        Assert.Equal([11, 12, 10], res.Data!.Matches.Select(m => m.Id).ToList());
    }

    [Fact]
    public async Task A_trip_that_has_gone_is_not_found()
    {
        // The other half of the spec's rule. The floor is the boarding grace
        // rather than "now", so a trip a few minutes late is still boarding —
        // but this morning's is gone.
        var uow = Scene();
        var now = DateTime.UtcNow;
        AddTrip(uow, 10, now - MatchRules.BoardingGrace - TimeSpan.FromMinutes(5));

        var res = await Search(uow).Search(Wanted(now));

        Assert.Equal(SearchMode.Hail, res.Data!.Mode);
    }

    [Fact]
    public async Task A_full_trip_is_not_found()
    {
        var uow = Scene();
        var now = DateTime.UtcNow;
        AddTrip(uow, 10, now.AddHours(2), seatsLeft: 0, status: TripStatus.Full);

        var res = await Search(uow).Search(Wanted(now));

        Assert.Equal(SearchMode.Hail, res.Data!.Mode);
    }

    [Fact]
    public async Task A_trip_with_too_few_seats_left_is_not_found()
    {
        var uow = Scene();
        var now = DateTime.UtcNow;
        AddTrip(uow, 10, now.AddHours(2), seatsLeft: 1);

        var input = Wanted(now);
        input.Seats = 3;
        var res = await Search(uow).Search(input);

        Assert.Equal(SearchMode.Hail, res.Data!.Mode);
    }

    // ── Setting off is what closes a trip to search ──────────────────────────

    [Fact]
    public async Task A_trip_leaves_search_when_the_driver_sets_off()
    {
        var uow = Scene();
        var now = DateTime.UtcNow;
        AddTrip(uow, 10, now.AddMinutes(20));

        var before = await Search(uow).Search(Wanted(now));
        Assert.Equal(SearchMode.Carpool, before.Data!.Mode);

        var depart = await Trips(uow).Depart(10);
        Assert.True(depart.Success);
        Assert.Equal(TripStatus.EnRoute, uow.Store<Trip>().Single().Status);

        var after = await Search(uow).Search(Wanted(now));
        Assert.Equal(SearchMode.Hail, after.Data!.Mode);
    }

    [Fact]
    public async Task Setting_off_moves_nobody_seat()
    {
        // EnRoute is the driver's own state, not a per-rider milestone: every
        // rider is still standing at their kerb until the car reaches it.
        var uow = Scene();
        AddTrip(uow, 10, DateTime.UtcNow.AddMinutes(20), seatsLeft: 2);
        uow.Store<Booking>().Add(new Booking
        {
            Id = 1, TripId = 10, RiderId = RiderId, Seats = 1, Status = BookingStatus.Confirmed,
        });

        await Trips(uow).Depart(10);

        Assert.Equal(BookingStatus.Confirmed, uow.Store<Booking>().Single().Status);
        Assert.Equal(TripStatus.EnRoute, uow.Store<Trip>().Single().Status);
    }

    [Fact]
    public async Task Setting_off_takes_the_driver_off_the_hail_board()
    {
        var uow = Scene();
        var driver = uow.Store<User>().First(u => u.Id == DriverId);
        driver.IsOnline = true;
        AddTrip(uow, 10, DateTime.UtcNow.AddMinutes(20));

        await Trips(uow).Depart(10);

        Assert.False(driver.IsOnline);
        Assert.True(DriverAvailabilityRules.IsEngaged(TripStatus.EnRoute));
    }

    [Fact]
    public async Task A_departed_trip_still_reaches_its_pickups_and_finishes()
    {
        // The whole run, to prove the new state is a stage and not a dead end.
        var uow = Scene();
        AddTrip(uow, 10, DateTime.UtcNow.AddMinutes(10), seatsLeft: 2);
        uow.Store<Booking>().Add(new Booking
        {
            Id = 1, TripId = 10, RiderId = RiderId, Seats = 1, Status = BookingStatus.Confirmed,
        });
        var trips = Trips(uow);

        Assert.True((await trips.Depart(10)).Success);
        Assert.Equal(TripStatus.EnRoute, uow.Store<Trip>().Single().Status);

        Assert.True((await trips.Arrive(10)).Success);
        Assert.Equal(TripStatus.Arrived, uow.Store<Trip>().Single().Status);

        Assert.True((await trips.Start(10)).Success);
        Assert.Equal(TripStatus.Active, uow.Store<Trip>().Single().Status);

        Assert.True((await trips.Complete(10)).Success);
        Assert.Equal(TripStatus.Completed, uow.Store<Trip>().Single().Status);
    }

    [Fact]
    public async Task A_driver_who_never_presses_set_off_can_still_collect()
    {
        // Refusing this would only teach drivers to tap a button that means
        // nothing to them. Posted → Arrived stays legal.
        var uow = Scene();
        AddTrip(uow, 10, DateTime.UtcNow.AddMinutes(10), seatsLeft: 2);
        uow.Store<Booking>().Add(new Booking
        {
            Id = 1, TripId = 10, RiderId = RiderId, Seats = 1, Status = BookingStatus.Confirmed,
        });

        var res = await Trips(uow).Arrive(10);

        Assert.True(res.Success);
        Assert.Equal(TripStatus.Arrived, uow.Store<Trip>().Single().Status);
    }

    [Fact]
    public async Task Setting_off_twice_is_refused()
    {
        var uow = Scene();
        AddTrip(uow, 10, DateTime.UtcNow.AddMinutes(20));
        var trips = Trips(uow);

        await trips.Depart(10);
        var again = await trips.Depart(10);

        Assert.False(again.Success);
        Assert.Equal(ErrorCode.Conflict, again.ErrorCode);
    }

    [Fact]
    public async Task A_trip_already_collecting_cannot_go_back_to_en_route()
    {
        var uow = Scene();
        AddTrip(uow, 10, DateTime.UtcNow.AddMinutes(10), seatsLeft: 2);
        uow.Store<Booking>().Add(new Booking
        {
            Id = 1, TripId = 10, RiderId = RiderId, Seats = 1, Status = BookingStatus.Confirmed,
        });
        var trips = Trips(uow);
        await trips.Arrive(10);

        var res = await trips.Depart(10);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.Conflict, res.ErrorCode);
    }
}
