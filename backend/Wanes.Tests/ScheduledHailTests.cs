using Microsoft.Extensions.Logging.Abstractions;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Notifications;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Requests;
using Wanes.Areas.Services.Search;
using Wanes.Areas.Services.Search.Models;
using Wanes.Areas.Services.Users.Availability;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.SSE;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// A hail is for the departure the rider searched for, not for the instant they
/// searched.
///
/// A hail used to mean "now" unconditionally: the request stored no wanted time,
/// the trip a driver created by accepting departed a pickup lead from the tap,
/// and everything downstream that needed the hail's hour — the driver's list,
/// the fan-out, the reverse match — read <c>RequestedAt</c> as a stand-in. That
/// is only ever right for the immediate case. A rider who searches for six this
/// evening, matches nothing and falls through to a hail is asking a driver to
/// take them at six.
///
/// These tests pin the difference, and pin that the immediate case did not move.
/// </summary>
public class ScheduledHailTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;

    /// <summary>Far enough out that no clash window reaches it from now.</summary>
    private static readonly TimeSpan ThisEvening = TimeSpan.FromHours(3);

    private static RideRequestService Requests(FakeUnitOfWork uow, int actingUserId) =>
        new(uow, new FakeSecurityManager(actingUserId), new FakeAuditService(),
            new FakeNotificationService(), new DriverAvailabilityService(uow.Repository<Trip>()),
            new FakeAppConfigurationService(),
            uow.Repository<RideRequest>(), uow.Repository<User>(), uow.Repository<Vehicle>(),
            uow.Repository<Trip>(), uow.Repository<Booking>());

    private static SearchService Search(FakeUnitOfWork uow) =>
        new(uow, new FakeSecurityManager(RiderId), new FakeAuditService(),
            new FakeNotificationService(), new FakeAppConfigurationService(),
            uow.Repository<Trip>(), uow.Repository<User>(), uow.Repository<Vehicle>(),
            uow.Repository<RideRequest>(), uow.Repository<Booking>());

    private static SearchInput Wanted(DateTime when) => new()
    {
        Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
        Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
        When = when,
        Seats = 1,
    };

    /// <summary>A verified driver with a car, and one open hail wanted at <paramref name="wantedDepartAt"/>.</summary>
    private static FakeUnitOfWork Scene(DateTime wantedDepartAt)
    {
        var uow = new FakeUnitOfWork();
        var driver = Build.Driver(DriverId);
        driver.IsOnline = true;
        driver.LastLocation = GeoFactory.Point(31.95, 35.92);
        uow.Store<User>().Add(driver);
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));
        uow.Store<RideRequest>().Add(new RideRequest
        {
            Id = 30,
            RiderId = RiderId,
            Seats = 1,
            OriginAddress = "A",
            Origin = GeoFactory.Point(31.95, 35.92),
            DestinationAddress = "B",
            Destination = GeoFactory.Point(32.01, 35.87),
            RadiusMeters = MatchRules.NearRadiusMeters,
            Status = RideRequestStatus.Open,
            RequestedAt = DateTime.UtcNow,
            WantedDepartAt = wantedDepartAt,
            // Long enough that none of these tests trip the deadline check; what
            // the window means is HailLifecycleTests' subject, not this file's.
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
        });
        return uow;
    }

    private static void GiveTheDriverATripAt(FakeUnitOfWork uow, DateTime departAt)
    {
        var other = Build.Trip(id: 77, driverId: DriverId, vehicleId: 1);
        other.DepartAt = departAt;
        uow.Store<Trip>().Add(other);
    }

    // ── Opening the hail ─────────────────────────────────────────────────────

    [Fact]
    public async Task Searching_for_a_later_hour_hails_for_that_hour()
    {
        var uow = new FakeUnitOfWork();
        var when = DateTime.UtcNow.Add(ThisEvening);

        var res = await Search(uow).Search(Wanted(when));

        Assert.Equal(SearchMode.Hail, res.Data!.Mode);
        var request = Assert.Single(uow.Store<RideRequest>());
        Assert.Equal(when, request.WantedDepartAt, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Searching_for_now_still_hails_a_pickup_lead_ahead()
    {
        // The ordinary case, unchanged: a rider hailing right now is not asking
        // for a car that materialises this second, and a departure already in
        // the past is one search can never offer back to anybody else.
        var uow = new FakeUnitOfWork();
        var before = DateTime.UtcNow;

        await Search(uow).Search(Wanted(before));

        var request = Assert.Single(uow.Store<RideRequest>());
        Assert.InRange(request.WantedDepartAt - before,
            MatchRules.HailPickupLead - TimeSpan.FromSeconds(5),
            MatchRules.HailPickupLead + TimeSpan.FromSeconds(5));
    }

    // ── Accepting it ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Accepting_a_scheduled_hail_creates_a_trip_leaving_at_the_wanted_hour()
    {
        var wanted = DateTime.UtcNow.Add(ThisEvening);
        var uow = Scene(wanted);

        var res = await Requests(uow, DriverId).Accept(30);

        Assert.True(res.Success);
        var trip = Assert.Single(uow.Store<Trip>());
        Assert.Equal(wanted, trip.DepartAt, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_wanted_hour_that_has_already_slipped_past_departs_a_pickup_lead_ahead()
    {
        // An old hail answered late. The wanted time is the floor's problem, not
        // the row's: stamping it verbatim would create a trip in the past, which
        // search will never offer and no second rider could ever join.
        var uow = Scene(DateTime.UtcNow.AddMinutes(-40));
        var before = DateTime.UtcNow;

        var res = await Requests(uow, DriverId).Accept(30);

        Assert.True(res.Success);
        var trip = Assert.Single(uow.Store<Trip>());
        Assert.InRange(trip.DepartAt - before,
            MatchRules.HailPickupLead - TimeSpan.FromSeconds(5),
            MatchRules.HailPickupLead + TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_driver_already_leaving_at_that_hour_cannot_take_it()
    {
        var wanted = DateTime.UtcNow.Add(ThisEvening);
        var uow = Scene(wanted);
        GiveTheDriverATripAt(uow, wanted.AddMinutes(5));

        var res = await Requests(uow, DriverId).Accept(30);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.DriverTripTimeConflict, res.ErrorCode);
        Assert.Single(uow.Store<Trip>());   // nothing new was created
    }

    [Fact]
    public async Task A_driver_busy_now_can_still_take_a_hail_for_this_evening()
    {
        // The behaviour this whole change turns on. The availability question is
        // about the moment being committed to, and a trip leaving in ten minutes
        // says nothing about whether the driver is free at six.
        var wanted = DateTime.UtcNow.Add(ThisEvening);
        var uow = Scene(wanted);
        GiveTheDriverATripAt(uow, DateTime.UtcNow.AddMinutes(10));

        var res = await Requests(uow, DriverId).Accept(30);

        Assert.True(res.Success);
        var trip = uow.Store<Trip>().Single(t => t.Id != 77);
        Assert.Equal(wanted, trip.DepartAt, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task A_driver_out_on_the_road_cannot_take_a_hail_for_any_hour()
    {
        // Being engaged outranks the clock: one driver, one car, and they are in
        // it. Unchanged by the scheduling rule, and deliberately so — it is the
        // same rule that stops them posting a trip for this evening mid-journey.
        var uow = Scene(DateTime.UtcNow.Add(ThisEvening));
        uow.Store<Trip>().Add(
            Build.Trip(id: 78, driverId: DriverId, vehicleId: 1, status: TripStatus.Active));

        var res = await Requests(uow, DriverId).Accept(30);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.DriverOnActiveTrip, res.ErrorCode);
    }

    // ── The driver's list agrees with what Accept will say ───────────────────

    [Fact]
    public async Task The_nearby_list_keeps_a_hail_the_drivers_own_trip_does_not_clash_with()
    {
        var uow = Scene(DateTime.UtcNow.Add(ThisEvening));
        GiveTheDriverATripAt(uow, DateTime.UtcNow.AddMinutes(10));

        var nearby = await Requests(uow, DriverId).GetNearby(31.95, 35.92, MatchRules.NearRadiusMeters);

        Assert.True(nearby.Success);
        Assert.Equal(30, Assert.Single(nearby.Data!).Id);
    }

    [Fact]
    public async Task The_nearby_list_drops_a_hail_that_clashes_with_the_drivers_own_trip()
    {
        var wanted = DateTime.UtcNow.Add(ThisEvening);
        var uow = Scene(wanted);
        GiveTheDriverATripAt(uow, wanted.AddMinutes(5));

        var nearby = await Requests(uow, DriverId).GetNearby(31.95, 35.92, MatchRules.NearRadiusMeters);

        Assert.True(nearby.Success);
        Assert.Empty(nearby.Data!);
    }

    [Fact]
    public async Task A_driver_out_on_the_road_is_shown_nothing()
    {
        var uow = Scene(DateTime.UtcNow.Add(ThisEvening));
        uow.Store<Trip>().Add(
            Build.Trip(id: 78, driverId: DriverId, vehicleId: 1, status: TripStatus.Active));

        var nearby = await Requests(uow, DriverId).GetNearby(31.95, 35.92, MatchRules.NearRadiusMeters);

        Assert.True(nearby.Success);
        Assert.Empty(nearby.Data!);
    }

    // ── The reverse match reads the same hour ────────────────────────────────

    [Fact]
    public async Task A_rider_hailing_for_this_evening_hears_about_an_evening_trip()
    {
        // The half of the reverse match that a "hails mean now" assumption made
        // unreachable: the hail was opened this morning, so RequestedAt put it
        // hours outside the window of the very trip that serves it.
        var wanted = DateTime.UtcNow.Add(ThisEvening);
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Rider(9));
        uow.Store<RideRequest>().Add(EveningHail(31, riderId: 9, wantedDepartAt: wanted));

        var trip = Build.Trip(id: 10, driverId: DriverId, vehicleId: 1, seatsTotal: 4);
        trip.DepartAt = wanted;

        Assert.Equal([9], await Told(uow, trip));
    }

    [Fact]
    public async Task A_rider_hailing_for_this_evening_hears_nothing_about_a_trip_leaving_now()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Rider(9));
        uow.Store<RideRequest>().Add(
            EveningHail(31, riderId: 9, wantedDepartAt: DateTime.UtcNow.Add(ThisEvening)));

        var trip = Build.Trip(id: 10, driverId: DriverId, vehicleId: 1, seatsTotal: 4);
        trip.DepartAt = DateTime.UtcNow.AddMinutes(5);

        Assert.Empty(await Told(uow, trip));
    }

    private static RideRequest EveningHail(int id, int riderId, DateTime wantedDepartAt) => new()
    {
        Id = id,
        RiderId = riderId,
        Seats = 1,
        OriginAddress = "A",
        Origin = GeoFactory.Point(31.95, 35.92),
        DestinationAddress = "B",
        Destination = GeoFactory.Point(32.01, 35.87),
        RadiusMeters = MatchRules.NearRadiusMeters,
        Status = RideRequestStatus.Open,
        RequestedAt = DateTime.UtcNow,
        WantedDepartAt = wantedDepartAt,
        ExpiresAt = DateTime.UtcNow.AddMinutes(10),
    };

    private static async Task<List<int>> Told(FakeUnitOfWork uow, Trip trip)
    {
        var service = new NotificationService(uow, new FakeSecurityManager(DriverId),
            new FakeFcmSender(), new SseConnectionManager(),
            NullLogger<NotificationService>.Instance,
            uow.Repository<UserNotification>(), uow.Repository<UserLogin>(), uow.Repository<User>(),
            uow.Repository<RideRequest>(), new DriverAvailabilityService(uow.Repository<Trip>()));

        await service.NotifyWaitingRiders(trip, Build.Driver(DriverId));
        return uow.Store<UserNotification>()
            .Where(n => n.Type == NotificationType.TripMatched)
            .Select(n => n.UserId)
            .ToList();
    }
}
