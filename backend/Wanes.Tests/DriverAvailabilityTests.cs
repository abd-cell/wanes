using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Requests;
using Wanes.Areas.Services.Trips;
using Wanes.Areas.Services.Trips.Models;
using Wanes.Areas.Services.Users.Availability;
using Wanes.Areas.Services.Users.Presence;
using Wanes.Areas.Services.Users.Presence.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// One driver drives one car: while they are out on a trip they are not
/// available, and they never hold two departures at the same moment. Every
/// door into a second ride is closed here — posting, editing, accepting a
/// hail, the hail list, the hail push and the presence flag itself.
/// </summary>
public class DriverAvailabilityTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;

    private static TripService Trips(FakeUnitOfWork uow, int driverId = DriverId) =>
        new(uow, new FakeSecurityManager(driverId), new FakeAuditService(), new FakeNotificationService(),
            new DriverAvailabilityService(uow.Repository<Trip>()),
            uow.Repository<Trip>(), uow.Repository<TripStatusHistory>(), uow.Repository<Vehicle>(),
            uow.Repository<User>(), uow.Repository<Booking>(), uow.Repository<RideRequest>());

    private static RideRequestService Requests(FakeUnitOfWork uow, int driverId = DriverId) =>
        new(uow, new FakeSecurityManager(driverId), new FakeAuditService(), new FakeNotificationService(),
            new DriverAvailabilityService(uow.Repository<Trip>()),
            new FakeAppConfigurationService(),
            uow.Repository<RideRequest>(), uow.Repository<User>(), uow.Repository<Vehicle>(),
            uow.Repository<Trip>(), uow.Repository<Booking>());

    private static PresenceService Presence(FakeUnitOfWork uow, int driverId = DriverId) =>
        new(uow, new FakeSecurityManager(driverId), new DriverAvailabilityService(uow.Repository<Trip>()),
            uow.Repository<User>());

    private static CreateTripInput Input(int minutesAhead = 60) => new()
    {
        VehicleId = 1,
        Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
        Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
        DepartAt = DateTime.UtcNow.AddMinutes(minutesAhead),
        SeatsTotal = 2,
    };

    /// <summary>A driver with a car, ready to be given trips to clash with.</summary>
    private static FakeUnitOfWork Driver(bool online = true)
    {
        var uow = new FakeUnitOfWork();
        var driver = Build.Driver(DriverId);
        driver.IsOnline = online;
        driver.LastLocation = GeoFactory.Point(31.95, 35.92);
        uow.Store<User>().Add(driver);
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));
        return uow;
    }

    private static Trip Held(int id, TripStatus status, int minutesAhead)
    {
        var trip = Build.Trip(id, driverId: DriverId, vehicleId: 1, status: status);
        trip.DepartAt = DateTime.UtcNow.AddMinutes(minutesAhead);
        return trip;
    }

    private static RideRequest OpenHail(int id = 30)
    {
        return new RideRequest
        {
            Id = id, RiderId = RiderId, Seats = 1,
            OriginAddress = "A", Origin = GeoFactory.Point(31.95, 35.92),
            DestinationAddress = "B", Destination = GeoFactory.Point(32.01, 35.87),
            RadiusMeters = 5000,
            Status = RideRequestStatus.Open,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        };
    }

    // ── Posting a trip ───────────────────────────────────────────────────────

    [Fact]
    public async Task Create_fails_while_the_driver_is_out_on_a_trip()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Active, minutesAhead: -20));

        var res = await Trips(uow).Create(Input(minutesAhead: 240));

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.DriverOnActiveTrip, res.ErrorCode);
    }

    [Fact]
    public async Task Create_fails_when_another_trip_departs_at_about_the_same_time()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Posted, minutesAhead: 70));

        var res = await Trips(uow).Create(Input(minutesAhead: 60));

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.DriverTripTimeConflict, res.ErrorCode);
    }

    [Fact]
    public async Task Create_allows_a_departure_outside_the_clash_window()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Posted, minutesAhead: 60));

        var res = await Trips(uow).Create(Input(minutesAhead: 100));

        Assert.True(res.Success);
    }

    [Fact]
    public async Task Create_ignores_finished_and_called_off_trips()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Completed, minutesAhead: 55));
        uow.Store<Trip>().Add(Held(11, TripStatus.Cancelled, minutesAhead: 65));

        var res = await Trips(uow).Create(Input(minutesAhead: 60));

        Assert.True(res.Success);
    }

    [Fact]
    public async Task Create_ignores_another_drivers_trip()
    {
        var uow = Driver();
        var other = Build.Trip(10, driverId: 99, vehicleId: 7);
        other.DepartAt = DateTime.UtcNow.AddMinutes(60);
        uow.Store<Trip>().Add(other);

        var res = await Trips(uow).Create(Input(minutesAhead: 60));

        Assert.True(res.Success);
    }

    [Fact]
    public async Task Update_may_move_a_trip_that_only_clashes_with_itself()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Posted, minutesAhead: 60));

        var res = await Trips(uow).Update(10, new UpdateTripInput
        {
            VehicleId = 1,
            Origin = new GeoPoint { Lat = 31.90, Lng = 35.90, Address = "A2" },
            Destination = new GeoPoint { Lat = 32.05, Lng = 35.80, Address = "B2" },
            DepartAt = DateTime.UtcNow.AddMinutes(70),
            SeatsTotal = 2,
        });

        Assert.True(res.Success);
    }

    // ── Taking a hail ────────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_fails_while_the_driver_is_out_on_a_trip()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Active, minutesAhead: -20));
        uow.Store<RideRequest>().Add(OpenHail());

        var res = await Requests(uow).Accept(30);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.DriverOnActiveTrip, res.ErrorCode);
        Assert.Equal(RideRequestStatus.Open, uow.Store<RideRequest>()[0].Status);
    }

    [Fact]
    public async Task Accept_fails_when_a_posted_trip_leaves_within_the_window()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Posted, minutesAhead: 15));
        uow.Store<RideRequest>().Add(OpenHail());

        var res = await Requests(uow).Accept(30);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.DriverTripTimeConflict, res.ErrorCode);
    }

    [Fact]
    public async Task Accept_succeeds_when_the_next_trip_is_hours_away()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Posted, minutesAhead: 180));
        uow.Store<RideRequest>().Add(OpenHail());

        var res = await Requests(uow).Accept(30);

        Assert.True(res.Success);
        Assert.Equal(RideRequestStatus.Matched, uow.Store<RideRequest>()[0].Status);
    }

    [Fact]
    public async Task GetNearby_is_empty_while_the_driver_is_out_on_a_trip()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Active, minutesAhead: -20));
        uow.Store<RideRequest>().Add(OpenHail());

        var res = await Requests(uow).GetNearby(31.95, 35.92, 5000);

        Assert.True(res.Success);
        Assert.Empty(res.Data!);
    }

    [Fact]
    public async Task GetNearby_still_lists_hails_for_a_free_driver()
    {
        var uow = Driver();
        uow.Store<RideRequest>().Add(OpenHail());

        var res = await Requests(uow).GetNearby(31.95, 35.92, 5000);

        Assert.True(res.Success);
        Assert.Single(res.Data!);
    }

    // ── Who the hail push reaches ────────────────────────────────────────────

    [Fact]
    public async Task BusyDrivers_skips_a_driver_on_a_trip_and_one_departing_soon()
    {
        var uow = new FakeUnitOfWork();
        var onTheRoad = Build.Trip(10, driverId: 2, vehicleId: 1, status: TripStatus.Active);
        onTheRoad.DepartAt = DateTime.UtcNow.AddMinutes(-20);
        var leavingSoon = Build.Trip(11, driverId: 3, vehicleId: 2);
        leavingSoon.DepartAt = DateTime.UtcNow.AddMinutes(10);
        var leavingLater = Build.Trip(12, driverId: 4, vehicleId: 3);
        leavingLater.DepartAt = DateTime.UtcNow.AddMinutes(120);
        uow.Store<Trip>().AddRange([onTheRoad, leavingSoon, leavingLater]);

        var svc = new DriverAvailabilityService(uow.Repository<Trip>());
        var busy = await svc.BusyDrivers([2, 3, 4, 5], DateTime.UtcNow);

        Assert.Equal([2, 3], busy.OrderBy(id => id));
    }

    // ── The presence flag ────────────────────────────────────────────────────

    [Fact]
    public async Task A_driver_on_a_trip_cannot_go_online()
    {
        var uow = Driver(online: false);
        uow.Store<Trip>().Add(Held(10, TripStatus.Active, minutesAhead: -20));

        var res = await Presence(uow).Update(new UpdateLocationInput { Lat = 31.9, Lng = 35.9, Online = true });

        Assert.True(res.Success);
        var driver = uow.Store<User>()[0];
        Assert.False(driver.IsOnline);
        Assert.NotNull(driver.LastLocationAt);   // the location is still taken, for tracking
    }

    [Fact]
    public async Task A_free_driver_still_goes_online()
    {
        var uow = Driver(online: false);
        uow.Store<Trip>().Add(Held(10, TripStatus.Posted, minutesAhead: 60));

        var res = await Presence(uow).Update(new UpdateLocationInput { Lat = 31.9, Lng = 35.9, Online = true });

        Assert.True(res.Success);
        Assert.True(uow.Store<User>()[0].IsOnline);
    }

    [Fact]
    public async Task Starting_a_trip_takes_the_driver_offline()
    {
        var uow = Driver();
        var trip = Held(10, TripStatus.Posted, minutesAhead: 5);
        trip.Driver = uow.Store<User>()[0];
        uow.Store<Trip>().Add(trip);
        uow.Store<Booking>().Add(new Booking
        {
            Id = 20, TripId = 10, RiderId = RiderId, Seats = 1, Status = BookingStatus.Confirmed,
        });

        var res = await Trips(uow).Start(10);

        Assert.True(res.Success);
        Assert.Equal(TripStatus.Active, uow.Store<Trip>()[0].Status);
        Assert.False(uow.Store<User>()[0].IsOnline);
    }
}
