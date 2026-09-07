using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Requests;
using Wanes.Areas.Services.Search;
using Wanes.Areas.Services.Search.Models;
using Wanes.Areas.Services.Users.Availability;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// What ends a hail, and what the drivers holding its card are told.
///
/// A hail leaves the Open state three ways — the rider cancels it, a driver
/// takes it, or its window runs out — and in all three it has to stop existing
/// for every other driver. Filtering it out of the next list read is only half
/// of that: the driver already looking at the sheet never re-reads. So each
/// exit also broadcasts a close, and these tests pin both halves.
/// </summary>
public class HailLifecycleTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;

    private static RideRequestService Requests(FakeUnitOfWork uow, FakeNotificationService notifications,
        int actingUserId) =>
        new(uow, new FakeSecurityManager(actingUserId), new FakeAuditService(), notifications,
            new DriverAvailabilityService(uow.Repository<Trip>()),
            new FakeAppConfigurationService(),
            uow.Repository<RideRequest>(), uow.Repository<User>(), uow.Repository<Vehicle>(),
            uow.Repository<Trip>(), uow.Repository<Booking>());

    private static SearchService Search(FakeUnitOfWork uow, FakeAppConfigurationService config) =>
        new(uow, new FakeSecurityManager(RiderId), new FakeAuditService(),
            new FakeNotificationService(), config,
            uow.Repository<Trip>(), uow.Repository<User>(), uow.Repository<Vehicle>(),
            uow.Repository<RideRequest>(), uow.Repository<Booking>());

    private static SearchInput Wanted() => new()
    {
        Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
        Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
        When = DateTime.UtcNow.AddMinutes(20),
        Seats = 1,
    };

    /// <summary>A verified driver with a car, and an open hail they could take.</summary>
    private static FakeUnitOfWork Scene(DateTime? expiresAt = null,
        RideRequestStatus status = RideRequestStatus.Open)
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
            Id = 30, RiderId = RiderId, Seats = 1,
            OriginAddress = "A", Origin = GeoFactory.Point(31.95, 35.92),
            DestinationAddress = "B", Destination = GeoFactory.Point(32.01, 35.87),
            RadiusMeters = 5000,
            Status = status,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(10),
        });
        return uow;
    }

    // ── The rider cancels ────────────────────────────────────────────────────

    [Fact]
    public async Task Cancelling_closes_the_request_on_the_drivers_screens()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();

        var res = await Requests(uow, notifications, RiderId).Cancel(30);

        Assert.True(res.Success);
        Assert.Equal(RideRequestStatus.Cancelled, uow.Store<RideRequest>()[0].Status);
        Assert.Contains($"request-30:{RideRequestStatus.Cancelled}", notifications.Sent);
    }

    [Fact]
    public async Task A_cancelled_request_is_no_longer_offered_to_drivers()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();
        await Requests(uow, notifications, RiderId).Cancel(30);

        var nearby = await Requests(uow, notifications, DriverId).GetNearby(31.95, 35.92, 5000);

        Assert.True(nearby.Success);
        Assert.Empty(nearby.Data!);
    }

    [Fact]
    public async Task A_cancelled_request_cannot_then_be_accepted()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();
        await Requests(uow, notifications, RiderId).Cancel(30);

        var accept = await Requests(uow, notifications, DriverId).Accept(30);

        Assert.False(accept.Success);
        Assert.Equal(ErrorCode.RequestNotOpen, accept.ErrorCode);
    }

    [Fact]
    public async Task Only_the_rider_who_opened_it_can_cancel()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();

        var res = await Requests(uow, notifications, DriverId).Cancel(30);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.RequestNotFound, res.ErrorCode);
        Assert.Equal(RideRequestStatus.Open, uow.Store<RideRequest>()[0].Status);
    }

    // ── A driver takes it ────────────────────────────────────────────────────

    [Fact]
    public async Task Accepting_closes_the_request_on_every_other_drivers_screen()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();

        var res = await Requests(uow, notifications, DriverId).Accept(30);

        Assert.True(res.Success);
        Assert.Contains($"request-30:{RideRequestStatus.Matched}", notifications.Sent);
    }

    // ── The window runs out ──────────────────────────────────────────────────

    [Fact]
    public async Task Accept_is_refused_once_the_window_has_passed()
    {
        // Status still reads Open: the sweeper runs on a timer, so there is
        // always a gap between the deadline and the row catching up.
        var uow = Scene(expiresAt: DateTime.UtcNow.AddSeconds(-1));

        var res = await Requests(uow, new FakeNotificationService(), DriverId).Accept(30);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.RequestNotOpen, res.ErrorCode);
        Assert.Empty(uow.Store<Trip>());
    }

    [Fact]
    public async Task ExpireDue_closes_a_hail_whose_window_has_passed()
    {
        var uow = Scene(expiresAt: DateTime.UtcNow.AddSeconds(-1));
        var notifications = new FakeNotificationService();

        var swept = await Requests(uow, notifications, RiderId).ExpireDue();

        Assert.Equal(1, swept);
        Assert.Equal(RideRequestStatus.Expired, uow.Store<RideRequest>()[0].Status);
        Assert.Contains($"request-30:{RideRequestStatus.Expired}", notifications.Sent);
    }

    [Fact]
    public async Task ExpireDue_leaves_a_hail_that_is_still_live()
    {
        var uow = Scene(expiresAt: DateTime.UtcNow.AddMinutes(5));
        var notifications = new FakeNotificationService();

        var swept = await Requests(uow, notifications, RiderId).ExpireDue();

        Assert.Equal(0, swept);
        Assert.Equal(RideRequestStatus.Open, uow.Store<RideRequest>()[0].Status);
        Assert.Empty(notifications.Sent);
    }

    [Fact]
    public async Task ExpireDue_leaves_a_request_that_already_ended()
    {
        // Cancelled and Matched are terminal; sweeping them would overwrite why
        // the request ended and fire a second close for the same id.
        var uow = Scene(expiresAt: DateTime.UtcNow.AddSeconds(-1),
            status: RideRequestStatus.Cancelled);
        var notifications = new FakeNotificationService();

        var swept = await Requests(uow, notifications, RiderId).ExpireDue();

        Assert.Equal(0, swept);
        Assert.Equal(RideRequestStatus.Cancelled, uow.Store<RideRequest>()[0].Status);
    }

    // ── The trip a driver creates by accepting ───────────────────────────────

    [Fact]
    public async Task Accepting_leaves_the_spare_seats_open_for_other_riders()
    {
        var uow = Scene();

        var res = await Requests(uow, new FakeNotificationService(), DriverId).Accept(30);

        Assert.True(res.Success);
        var trip = Assert.Single(uow.Store<Trip>());
        // The hailing rider took one of four; the rest are a normal carpool.
        Assert.Equal(4, trip.SeatsTotal);
        Assert.Equal(3, trip.SeatsLeft);
        Assert.Equal(TripStatus.Posted, trip.Status);
    }

    [Fact]
    public async Task The_trip_a_driver_creates_by_accepting_is_offered_to_other_riders()
    {
        // The regression this pins: DepartAt used to be stamped UtcNow, and
        // search only offers a departure in the future — so by the time anyone
        // looked, the trip had already "left" and no second rider could ever
        // find it. The premise of the product is that one trip carries several.
        var uow = Scene();
        await Requests(uow, new FakeNotificationService(), DriverId).Accept(30);

        var other = new FakeSecurityManager(9);
        var search = new SearchService(uow, other, new FakeAuditService(),
            new FakeNotificationService(), new FakeAppConfigurationService(),
            uow.Repository<Trip>(), uow.Repository<User>(), uow.Repository<Vehicle>(),
            uow.Repository<RideRequest>(), uow.Repository<Booking>());

        var res = await search.Search(Wanted());

        Assert.Equal(SearchMode.Carpool, res.Data!.Mode);
        Assert.Single(res.Data.Matches);
    }

    [Fact]
    public async Task Accepting_departs_a_pickup_lead_ahead_rather_than_this_instant()
    {
        var uow = Scene();
        var before = DateTime.UtcNow;

        await Requests(uow, new FakeNotificationService(), DriverId).Accept(30);

        var trip = Assert.Single(uow.Store<Trip>());
        Assert.True(trip.DepartAt > before,
            "the driver still has to reach the pickup, so departure is ahead of now");
        Assert.InRange(trip.DepartAt - before,
            MatchRules.HailPickupLead - TimeSpan.FromSeconds(5),
            MatchRules.HailPickupLead + TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Accepting_offers_the_spare_seats_to_riders_already_waiting()
    {
        // Posting a trip tells riders sitting on a matching open hail. Accepting
        // one creates just as real a trip, with just as real a set of spare
        // seats, so it has to do the same — otherwise nobody else ever hears.
        var uow = Scene();
        var notifications = new FakeNotificationService();

        await Requests(uow, notifications, DriverId).Accept(30);

        var trip = Assert.Single(uow.Store<Trip>());
        Assert.Contains($"waiting:{trip.Id}", notifications.Sent);
    }

    [Fact]
    public async Task A_hail_that_fills_the_car_offers_nothing_to_anyone()
    {
        // Capacity 4, all four asked for: nothing left, so the waiting riders
        // must not be pinged about a trip they could not board.
        var uow = Scene();
        uow.Store<RideRequest>()[0].Seats = 4;
        var notifications = new FakeNotificationService();

        await Requests(uow, notifications, DriverId).Accept(30);

        var trip = Assert.Single(uow.Store<Trip>());
        Assert.Equal(0, trip.SeatsLeft);
        Assert.Equal(TripStatus.Full, trip.Status);
        Assert.DoesNotContain(notifications.Sent, sent => sent.StartsWith("waiting:"));
    }

    // ── The window itself is configuration ───────────────────────────────────

    [Fact]
    public async Task A_new_hail_expires_after_the_configured_window()
    {
        var uow = new FakeUnitOfWork();
        var search = Search(uow, new FakeAppConfigurationService { HailRequestTtlMinutes = 45 });

        var res = await search.Search(Wanted());

        Assert.Equal(SearchMode.Hail, res.Data!.Mode);
        var request = Assert.Single(uow.Store<RideRequest>());
        var window = request.ExpiresAt!.Value - request.RequestedAt;
        Assert.InRange(window.TotalMinutes, 44.5, 45.5);
    }

    [Theory]
    [InlineData(0, MatchRules.MinHailTtlMinutes)]
    [InlineData(9999, MatchRules.MaxHailTtlMinutes)]
    public async Task An_out_of_range_window_is_clamped(int configured, int expectedMinutes)
    {
        var uow = new FakeUnitOfWork();
        var search = Search(uow, new FakeAppConfigurationService { HailRequestTtlMinutes = configured });

        await search.Search(Wanted());

        var request = Assert.Single(uow.Store<RideRequest>());
        var window = request.ExpiresAt!.Value - request.RequestedAt;
        Assert.InRange(window.TotalMinutes, expectedMinutes - 0.5, expectedMinutes + 0.5);
    }
}
