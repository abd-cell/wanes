using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Search;
using Wanes.Areas.Services.Search.Models;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// A carpool match needs the driver to actually be around here.
///
/// Matching the rider's pickup against the trip's *planned* origin alone makes a
/// notional match: the pickup point can be next door while the driver is an hour
/// away. So when we know where the driver is, that has to be near the rider too.
///
/// "When we know" is the subtlety. A driver only reports a position while the app
/// is running, so most trips posted for a future day carry a stale fix or none —
/// requiring one would hide them all. A missing or old fix is therefore ignored
/// and the trip is judged on its planned origin exactly as before.
///
/// <para><b>What is not tested here, and cannot be.</b> Whether a *fresh* fix is
/// near enough is a radius question, and the doubles cannot answer one: they run
/// NetTopologySuite in memory, where <c>IsWithinDistance</c> is planar and reads
/// its argument as degrees, so a 5 000 m radius becomes 5 000 degrees and every
/// point on earth is "within". A test asserting exclusion would fail for a reason
/// unrelated to the rule, and one asserting inclusion would pass vacuously.
/// That half is verified against SQL Server geography instead.</para>
///
/// So what is pinned below is the half that is honest in memory: the freshness
/// gate as a rule of its own, and that a driver with no usable fix is *kept*
/// rather than dropped — which is the regression that would matter most, because
/// it would empty search of every scheduled trip.
/// </summary>
public class LiveLocationMatchTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;

    private const double NearLat = 31.9539;
    private const double NearLng = 35.9106;
    private const double DestLat = 32.0100;
    private const double DestLng = 35.8700;

    private static SearchService Search(FakeUnitOfWork uow) =>
        new(uow, new FakeSecurityManager(RiderId), new FakeAuditService(),
            new FakeNotificationService(), new FakeAppConfigurationService(),
            uow.Repository<Trip>(), uow.Repository<User>(), uow.Repository<Vehicle>(),
            uow.Repository<RideRequest>(), uow.Repository<Booking>());

    private static SearchInput Wanted() => new()
    {
        Origin = new GeoPoint { Lat = NearLat, Lng = NearLng, Address = "A" },
        Destination = new GeoPoint { Lat = DestLat, Lng = DestLng, Address = "B" },
        When = DateTime.UtcNow,
        Seats = 1,
    };

    private static FakeUnitOfWork Scene(bool withPosition, DateTime? reportedAt)
    {
        var uow = new FakeUnitOfWork();
        var driver = Build.Driver(DriverId);
        if (withPosition) driver.LastLocation = GeoFactory.Point(NearLat, NearLng);
        driver.LastLocationAt = reportedAt;
        uow.Store<User>().Add(driver);
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));

        var trip = Build.Trip(10, driverId: DriverId, vehicleId: 1, seatsTotal: 3);
        trip.Origin = GeoFactory.Point(NearLat, NearLng);
        trip.Destination = GeoFactory.Point(DestLat, DestLng);
        trip.DepartAt = DateTime.UtcNow.AddMinutes(20);
        trip.SeatsLeft = 3;
        // The fake repositories ignore Include shapers, so the navigation the
        // filter reads is wired by hand.
        trip.Driver = driver;
        uow.Store<Trip>().Add(trip);
        return uow;
    }

    private static async Task<SearchMode> ModeFor(FakeUnitOfWork uow) =>
        (await Search(uow).Search(Wanted())).Data!.Mode;

    // ── The freshness gate, as a rule ────────────────────────────────────────

    [Fact]
    public void A_position_reported_just_now_is_live()
    {
        var now = DateTime.UtcNow;

        Assert.True(MatchRules.IsLiveFix(now, now));
        Assert.True(MatchRules.IsLiveFix(now - TimeSpan.FromMinutes(1), now));
    }

    [Fact]
    public void A_position_inside_the_window_is_live_and_one_outside_is_not()
    {
        var now = DateTime.UtcNow;

        Assert.True(MatchRules.IsLiveFix(now - MatchRules.LiveFixWindow + TimeSpan.FromSeconds(1), now));
        Assert.False(MatchRules.IsLiveFix(now - MatchRules.LiveFixWindow, now));
        Assert.False(MatchRules.IsLiveFix(now - MatchRules.LiveFixWindow - TimeSpan.FromMinutes(1), now));
    }

    [Fact]
    public void A_position_with_no_timestamp_is_not_live()
    {
        // A row written before the timestamp existed, or a partial update. An
        // unaged position cannot be called current, so it must not be trusted
        // enough to exclude a trip.
        Assert.False(MatchRules.IsLiveFix(null, DateTime.UtcNow));
    }

    [Fact]
    public void The_window_and_the_floor_agree()
    {
        // The query compares against the floor because IsLiveFix cannot be
        // translated to SQL. If these two ever disagreed, search and every other
        // caller would answer the same question differently.
        var now = DateTime.UtcNow;
        var floor = MatchRules.LiveFixFloor(now);

        Assert.Equal(now - MatchRules.LiveFixWindow, floor);
        Assert.True(MatchRules.IsLiveFix(floor.AddSeconds(1), now));
        Assert.False(MatchRules.IsLiveFix(floor.AddSeconds(-1), now));
    }

    // ── A driver with no usable fix is kept ──────────────────────────────────

    [Fact]
    public async Task A_driver_who_has_never_reported_still_matches()
    {
        // Posted the trip, closed the app. The commonest case by far for a
        // scheduled trip, and it must not be the one that disappears.
        var uow = Scene(withPosition: false, reportedAt: null);

        Assert.Equal(SearchMode.Carpool, await ModeFor(uow));
    }

    [Fact]
    public async Task A_driver_whose_fix_has_gone_stale_still_matches()
    {
        var uow = Scene(withPosition: true,
            reportedAt: DateTime.UtcNow - MatchRules.LiveFixWindow - TimeSpan.FromMinutes(5));

        Assert.Equal(SearchMode.Carpool, await ModeFor(uow));
    }

    [Fact]
    public async Task A_position_without_a_timestamp_does_not_exclude_the_trip()
    {
        var uow = Scene(withPosition: true, reportedAt: null);

        Assert.Equal(SearchMode.Carpool, await ModeFor(uow));
    }

    [Fact]
    public async Task A_driver_reporting_from_the_rider_pickup_matches()
    {
        var uow = Scene(withPosition: true, reportedAt: DateTime.UtcNow);

        Assert.Equal(SearchMode.Carpool, await ModeFor(uow));
    }
}
