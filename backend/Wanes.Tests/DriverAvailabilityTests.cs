using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.RideRequests;
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
/// door into a second ride is closed here — posting, editing, claiming a
/// rider-posted trip, the driver's board, the push, and the presence flag
/// itself.
/// </summary>
public class DriverAvailabilityTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;

    private static TripService Trips(FakeUnitOfWork uow, int driverId = DriverId) =>
        Make.Trips(uow, driverId);

    private static RideRequestService Requests(FakeUnitOfWork uow, int driverId = DriverId) =>
        Make.Requests(uow, driverId);

    private static DriverInterestService Interests(FakeUnitOfWork uow, int driverId = DriverId) =>
        Make.Interests(uow, driverId);

    private static PresenceService Presence(FakeUnitOfWork uow, int driverId = DriverId) =>
        new(uow, new FakeSecurityManager(driverId), new DriverAvailabilityService(uow.Repository<Trip>(), new FakeSecurityManager()),
            uow.Repository<User>());

    private static DriverAvailabilityService Availability(FakeUnitOfWork uow, int driverId = DriverId) =>
        new(uow.Repository<Trip>(), new FakeSecurityManager(driverId));

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

    /// <summary>
    /// An open posting a driver could take, two hours out.
    ///
    /// Far enough ahead that the lead-time rule is satisfied, so the departures
    /// these tests clash against are the only variable — a posting for "now"
    /// would be refused for a different reason entirely.
    /// </summary>
    private static RideRequest OpenRequest(FakeUnitOfWork uow, int id = 30, int minutesAhead = 120) =>
        Build.Demand(uow, id, RiderId, seats: 1, departAt: DateTime.UtcNow.AddMinutes(minutesAhead));

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

    // ── Claiming a rider-posted trip ─────────────────────────────────────────

    [Fact]
    public async Task Claim_fails_while_the_driver_is_out_on_a_trip()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Active, minutesAhead: -20));
        OpenRequest(uow);

        var res = await Interests(uow).ExpressInterest(30, Offer.Shared());

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.DriverOnActiveTrip, res.ErrorCode);
        // The request is untouched — and it is not in the trips table at all
        // any more, which is the point of it being its own object.
        Assert.Equal(RideRequestStatus.Open, uow.Store<RideRequest>().Single(r => r.Id == 30).Status);
    }

    [Fact]
    public async Task Offering_fails_when_a_posted_trip_leaves_within_the_window()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Posted, minutesAhead: 125));
        OpenRequest(uow);

        var res = await Interests(uow).ExpressInterest(30, Offer.Shared());

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.DriverTripTimeConflict, res.ErrorCode);
    }

    [Fact]
    public async Task Offering_succeeds_when_the_next_trip_is_hours_away()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Posted, minutesAhead: 480));
        OpenRequest(uow);

        var res = await Interests(uow).ExpressInterest(30, Offer.Shared());

        Assert.True(res.Success);
        Assert.Equal(TripStatus.Posted, uow.Store<Trip>()[0].Status);
    }

    [Fact]
    public async Task GetNearby_is_empty_while_the_driver_is_out_on_a_trip()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Active, minutesAhead: -20));
        OpenRequest(uow);

        var res = await Requests(uow).GetNearby(31.95, 35.92, 5000);

        Assert.True(res.Success);
        Assert.Empty(res.Data!);
    }

    [Fact]
    public async Task GetNearby_still_lists_postings_for_a_free_driver()
    {
        var uow = Driver();
        OpenRequest(uow);

        var res = await Requests(uow).GetNearby(31.95, 35.92, 5000);

        Assert.True(res.Success);
        Assert.Single(res.Data!);
    }

    // ── Who the push reaches ─────────────────────────────────────────────────

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

        var svc = new DriverAvailabilityService(uow.Repository<Trip>(), new FakeSecurityManager());
        var busy = await svc.BusyDrivers([2, 3, 4, 5], DateTime.UtcNow);

        Assert.Equal([2, 3], busy.OrderBy(id => id));
    }

    // ── The diary a client greys its picker with ─────────────────────────────

    [Fact]
    public async Task My_schedule_lists_the_departures_already_promised()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Posted, minutesAhead: 60));
        uow.Store<Trip>().Add(Held(11, TripStatus.Full, minutesAhead: 300));

        // Neither of these holds a slot any more.
        uow.Store<Trip>().Add(Held(12, TripStatus.Completed, minutesAhead: 120));
        uow.Store<Trip>().Add(Held(13, TripStatus.Cancelled, minutesAhead: 180));

        var res = await Availability(uow).GetMySchedule();

        Assert.True(res.Success);
        Assert.Equal(2, res.Data!.CommittedDepartures.Count);
        Assert.False(res.Data.IsEngaged);

        // The window travels with the list: a client that carried its own copy
        // of the number would eventually grey out a slot the API would take.
        Assert.Equal((int)DriverAvailabilityRules.ClashWindow.TotalMinutes, res.Data.ClashWindowMinutes);
    }

    [Fact]
    public async Task My_schedule_reports_a_driver_who_is_out_on_the_road()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Active, minutesAhead: -20));

        var res = await Availability(uow).GetMySchedule();

        // A different sentence to a clash: not "not then" but "not yet".
        Assert.True(res.Data!.IsEngaged);
    }

    [Fact]
    public async Task My_schedule_leaves_out_the_trip_being_edited()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Posted, minutesAhead: 60));

        var res = await Availability(uow).GetMySchedule(ignoreTripId: 10);

        // Otherwise moving a trip's time would find the trip itself blocking
        // every slot around where it already is.
        Assert.Empty(res.Data!.CommittedDepartures);
    }

    [Fact]
    public async Task My_schedule_is_only_ever_the_callers_own()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.Posted, minutesAhead: 60));

        var somebodyElse = new DriverAvailabilityService(
            uow.Repository<Trip>(), new FakeSecurityManager(RiderId));
        var res = await somebodyElse.GetMySchedule();

        Assert.Empty(res.Data!.CommittedDepartures);
    }

    /// <summary>
    /// A driver who has set off holds their slot like any other. The three
    /// queries that answer this used to spell the status list out by hand and
    /// had all dropped <see cref="TripStatus.EnRoute"/> — the one status where
    /// the driver is already on the road — so the trip they were driving to
    /// blocked nothing and a second departure could be promised on top of it.
    /// </summary>
    [Fact]
    public async Task A_driver_on_their_way_to_a_pickup_cannot_promise_another_departure()
    {
        var uow = Driver();
        uow.Store<Trip>().Add(Held(10, TripStatus.EnRoute, minutesAhead: 5));

        var res = await Trips(uow).Create(Input(minutesAhead: 240));

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.DriverOnActiveTrip, res.ErrorCode);

        var schedule = await Availability(uow).GetMySchedule();
        Assert.True(schedule.Data!.IsEngaged);
        Assert.Single(schedule.Data.CommittedDepartures);
    }

    [Fact]
    public async Task BusyDrivers_counts_a_driver_who_has_set_off()
    {
        var uow = new FakeUnitOfWork();
        var enRoute = Build.Trip(10, driverId: 2, vehicleId: 1, status: TripStatus.EnRoute);
        enRoute.DepartAt = DateTime.UtcNow.AddMinutes(5);
        uow.Store<Trip>().Add(enRoute);

        var svc = new DriverAvailabilityService(uow.Repository<Trip>(), new FakeSecurityManager());
        var busy = await svc.BusyDrivers([2, 3], DateTime.UtcNow.AddHours(4));

        // Four hours out is no clash at all — this driver is excluded because
        // they are already driving, which the query used not to see.
        Assert.Equal([2], busy);
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
