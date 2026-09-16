using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.RideRequests;
using Wanes.Areas.Services.RideRequests.Models;
using Wanes.Areas.Services.Users.Availability;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// What a hail-accepted trip is listed at.
///
/// The driver names it when they accept — the app requires a figure and
/// pre-fills the distance estimate, so what reaches here is a price a driver
/// looked at and confirmed. It is stored verbatim, exactly as a posted trip's
/// own price is.
///
/// The admin's rates are the *fallback*, for a call that carried no price at all
/// (an older build, a retry whose body was lost). They are not a second pricing
/// model, they are the guarantee that this never produces the one kind of trip
/// in search with a blank where every other row shows a figure.
/// </summary>
public class FarePricingTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;

    // Amman city centre to Zarqa: far enough apart that the per-km half of the
    // fare is the part doing the work, so a wrong rate cannot hide in rounding.
    private const double OriginLat = 31.9539;
    private const double OriginLng = 35.9106;
    private const double DestLat = 32.0728;
    private const double DestLng = 36.0876;

    private static DriverInterestService Interests(FakeUnitOfWork uow,
        FakeAppConfigurationService config) =>
        Make.Interests(uow, DriverId, config: config);

    private static FakeUnitOfWork Scene()
    {
        var uow = new FakeUnitOfWork();
        var driver = Build.Driver(DriverId);
        driver.IsOnline = true;
        driver.LastLocation = GeoFactory.Point(OriginLat, OriginLng);
        uow.Store<User>().Add(driver);
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));
        var posting = Build.Demand(uow, 30, RiderId, departAt: DateTime.UtcNow.AddHours(3));
        posting.Origin = GeoFactory.Point(OriginLat, OriginLng);
        posting.Destination = GeoFactory.Point(DestLat, DestLng);
        return uow;
    }

    private static readonly double RouteKm =
        GeoDistance.Km(OriginLat, OriginLng, DestLat, DestLng);

    // ── The driver's own price ───────────────────────────────────────────────

    [Fact]
    public async Task The_price_the_driver_names_is_what_the_trip_lists_at()
    {
        var uow = Scene();

        var res = await Interests(uow, new FakeAppConfigurationService())
            .ExpressInterest(30, new ExpressInterestInput { PricePerSeat = 7.25m });

        Assert.True(res.Success);
        Assert.Equal(7.25m, Assert.Single(uow.Store<Trip>()).PricePerSeat);
    }

    [Fact]
    public async Task The_driver_price_beats_the_configured_rates()
    {
        // The rates are a fallback, not a cap or a floor. A driver charging well
        // under what the platform would have derived is making an offer, and it
        // has to survive.
        var config = new FakeAppConfigurationService { FareBaseAmount = 50m, FarePerKm = 10m };
        var uow = Scene();

        await Interests(uow, config).ExpressInterest(30, new ExpressInterestInput { PricePerSeat = 2m });

        Assert.Equal(2m, Assert.Single(uow.Store<Trip>()).PricePerSeat);
    }

    [Fact]
    public async Task A_driver_charging_nothing_is_a_free_seat_not_a_missing_price()
    {
        // Zero and unset have to stay tellable apart: a colleague giving someone
        // a lift is a real answer, and coercing it to a derived fare would
        // charge for a favour.
        var uow = Scene();

        await Interests(uow, new FakeAppConfigurationService())
            .ExpressInterest(30, new ExpressInterestInput { PricePerSeat = 0m });

        Assert.Equal(0m, Assert.Single(uow.Store<Trip>()).PricePerSeat);
    }

    [Fact]
    public async Task A_nonsense_price_is_clamped_rather_than_stored()
    {
        var uow = Scene();

        await Interests(uow, new FakeAppConfigurationService())
            .ExpressInterest(30, new ExpressInterestInput { PricePerSeat = -5m });

        Assert.Equal(0m, Assert.Single(uow.Store<Trip>()).PricePerSeat);
    }

    [Fact]
    public async Task A_driver_price_is_rounded_to_what_the_column_holds()
    {
        var uow = Scene();

        await Interests(uow, new FakeAppConfigurationService())
            .ExpressInterest(30, new ExpressInterestInput { PricePerSeat = 3.456m });

        Assert.Equal(3.46m, Assert.Single(uow.Store<Trip>()).PricePerSeat);
    }

    // ── The fallback, for a call that named no price ─────────────────────────

    [Fact]
    public async Task A_call_with_no_price_falls_back_to_the_configured_rates()
    {
        var config = new FakeAppConfigurationService { FareBaseAmount = 3m, FarePerKm = 0.5m };
        var uow = Scene();

        var res = await Interests(uow, config).ExpressInterest(30);

        Assert.True(res.Success);
        var trip = Assert.Single(uow.Store<Trip>());
        Assert.Equal(FareRules.PerSeat(RouteKm, 3m, 0.5m), trip.PricePerSeat);
    }

    [Fact]
    public async Task Changing_the_rates_changes_what_the_next_trip_lists_at()
    {
        // The point of the rates being configuration rather than constants: an
        // admin doubling the per-km rate has to move the price, or the setting
        // is decoration.
        var cheap = await PriceWith(new FakeAppConfigurationService { FareBaseAmount = 0m, FarePerKm = 1m });
        var dear = await PriceWith(new FakeAppConfigurationService { FareBaseAmount = 0m, FarePerKm = 2m });

        Assert.NotNull(cheap);
        Assert.Equal(cheap!.Value * 2, dear);
    }

    [Fact]
    public async Task A_zero_rate_prices_every_ride_at_the_flag_fall()
    {
        // A legitimate configuration for a small town, and it must not read as
        // "unpriced": null and 2.50 mean different things to a client.
        var price = await PriceWith(new FakeAppConfigurationService { FareBaseAmount = 2.5m, FarePerKm = 0m });

        Assert.Equal(2.50m, price);
    }

    [Fact]
    public async Task The_price_is_never_left_blank()
    {
        // The regression this exists for. A hail-accepted trip used to be created
        // with no price at all, so it listed with an empty figure next to posted
        // trips that all showed one.
        var price = await PriceWith(new FakeAppConfigurationService());

        Assert.NotNull(price);
        Assert.True(price > 0);
    }

    [Fact]
    public void The_derived_price_is_per_seat_not_per_trip()
    {
        // A rider taking three seats pays three times this. Multiplying here
        // would price the whole party into one seat's worth of column.
        var one = FareRules.PerSeat(10, 2m, 1m);

        Assert.Equal(12m, one);
    }

    [Fact]
    public void The_derived_price_fits_the_column_it_is_stored_in()
    {
        // PricePerSeat is decimal(10,2). Deriving more precision than can be
        // saved would mean the figure quoted and the figure stored disagreed.
        var price = FareRules.PerSeat(7.7777, 1.111m, 1.111m);

        Assert.Equal(Math.Round(price, 2), price);
    }

    [Fact]
    public void A_nonsense_distance_still_prices_at_the_flag_fall()
    {
        Assert.Equal(2.5m, FareRules.PerSeat(0, 2.5m, 1m));
        Assert.Equal(2.5m, FareRules.PerSeat(double.NaN, 2.5m, 1m));
        Assert.Equal(2.5m, FareRules.PerSeat(-40, 2.5m, 1m));
    }

    [Fact]
    public void Rates_are_clamped_to_something_a_trip_can_list_at()
    {
        Assert.Equal(0m, FareRules.RateFor(-1m));
        Assert.Equal(FareRules.MaxRate, FareRules.RateFor(FareRules.MaxRate + 1m));
        Assert.Equal(1.20m, FareRules.RateFor(1.20m));
    }

    [Fact]
    public void Distance_is_measured_in_kilometres_not_degrees()
    {
        // The trap GeoDistance exists for: NetTopologySuite's own Distance runs
        // planar maths on lat/lng in memory and answers in degrees, which is a
        // plausible-looking number that would price a 25 km ride as a 0.2 km one.
        var planar = GeoFactory.Point(OriginLat, OriginLng)
            .Distance(GeoFactory.Point(DestLat, DestLng));

        Assert.InRange(RouteKm, 20, 30);
        Assert.True(planar < 1, "the planar figure is degrees — proof it must not be used as km");
    }

    private static async Task<decimal?> PriceWith(FakeAppConfigurationService config)
    {
        var uow = Scene();
        var res = await Interests(uow, config).ExpressInterest(30);
        Assert.True(res.Success);
        return Assert.Single(uow.Store<Trip>()).PricePerSeat;
    }
}
