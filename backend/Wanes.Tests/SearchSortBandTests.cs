using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Search;
using Wanes.Areas.Services.Search.Models;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// The rider's sort, against the band rule.
///
/// A sort orders the results *within* each band and never across them: a trip
/// going the rider's way beats one that merely passes it, however cheap or
/// early the passing one is. That is the whole reason the home page can render
/// the bands as sections and still call the result one ranking.
///
/// The rule used to be arithmetic rather than structure — every sort folded a
/// constant tier penalty into one number and trusted the sort key to stay
/// smaller than it. Two keys did not: a departure expressed in minutes-since-
/// year-one dwarfs the penalty outright, and an unpriced trip was pushed to
/// <c>decimal.MaxValue / 2</c>, which dwarfs everything. So "cheapest" could
/// answer with a corridor trip above a direct one, and "leaving soonest" did
/// whenever the two were more than a few hours apart.
///
/// These tests are about the crossing, not the ordering — <c>SearchAndRatingTests</c>
/// covers the within-band order.
/// </summary>
public class SearchSortBandTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;

    // The rider's hop, and a long leg that runs straight past it.
    private const double PickupLat = 31.9520, PickupLng = 35.80;
    private const double DropoffLat = 31.9520, DropoffLng = 36.00;

    /// <summary>
    /// One trip going exactly the rider's way (Direct) and one long leg that
    /// merely passes them (OnTheWay), so every sort below has one of each to
    /// order.
    /// </summary>
    private static FakeUnitOfWork Scene(
        DateTime directDepartsAt,
        DateTime passingDepartsAt,
        decimal? directPrice = 5m,
        decimal? passingPrice = 5m)
    {
        var uow = new FakeUnitOfWork();
        var driver = Build.Driver(DriverId);
        uow.Store<User>().Add(driver);
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));

        var directOrigin = GeoFactory.Point(PickupLat, PickupLng);
        var directDestination = GeoFactory.Point(DropoffLat, DropoffLng);
        var direct = Build.Trip(10, driverId: DriverId, vehicleId: 1, seatsTotal: 3);
        direct.Driver = driver;
        direct.Origin = directOrigin;
        direct.Destination = directDestination;
        direct.Route = GeoFactory.Line(directOrigin, directDestination);
        direct.DepartAt = directDepartsAt;
        direct.PricePerSeat = directPrice;
        uow.Store<Trip>().Add(direct);

        // Same line, but starting and ending far outside the rider's radius, so
        // it can only ever match on the corridor.
        var passingOrigin = GeoFactory.Point(31.95, 35.20);
        var passingDestination = GeoFactory.Point(31.95, 36.60);
        var passing = Build.Trip(11, driverId: DriverId, vehicleId: 1, seatsTotal: 3);
        passing.Driver = driver;
        passing.Origin = passingOrigin;
        passing.Destination = passingDestination;
        passing.Route = GeoFactory.Line(passingOrigin, passingDestination);
        passing.DepartAt = passingDepartsAt;
        passing.PricePerSeat = passingPrice;
        uow.Store<Trip>().Add(passing);

        return uow;
    }

    private static async Task<List<SearchMatch>> Search(FakeUnitOfWork uow, SearchSort sort) =>
        (await Make.Search(uow, RiderId).Search(new SearchInput
        {
            Origin = new GeoPoint { Lat = PickupLat, Lng = PickupLng, Address = "A" },
            Destination = new GeoPoint { Lat = DropoffLat, Lng = DropoffLng, Address = "B" },
            When = DateTime.UtcNow.AddHours(1),
            Seats = 1,
            SortBy = sort,
        })).Data!.Matches;

    /// <summary>Both bands are present, and the direct one leads.</summary>
    private static void AssertDirectLeads(List<SearchMatch> matches)
    {
        Assert.Equal(2, matches.Count);
        Assert.Equal(SearchTier.Direct, matches[0].Tier);
        Assert.Equal(10, matches[0].Trip.Id);
        Assert.Equal(SearchTier.OnTheWay, matches[1].Tier);
    }

    [Fact]
    public async Task Departure_sort_keeps_a_direct_trip_above_a_passing_one()
    {
        // A day between them, which is well past the point where the old tier
        // penalty stopped outweighing the departure key.
        var uow = Scene(
            directDepartsAt: DateTime.UtcNow.AddHours(25),
            passingDepartsAt: DateTime.UtcNow.AddHours(1));

        AssertDirectLeads(await Search(uow, SearchSort.Departure));
    }

    [Fact]
    public async Task Price_sort_keeps_a_direct_trip_above_a_cheaper_passing_one()
    {
        var uow = Scene(
            directDepartsAt: DateTime.UtcNow.AddHours(1),
            passingDepartsAt: DateTime.UtcNow.AddHours(1),
            directPrice: 20m,
            passingPrice: 1m);

        AssertDirectLeads(await Search(uow, SearchSort.Price));
    }

    [Fact]
    public async Task Price_sort_keeps_an_unpriced_direct_trip_above_a_priced_passing_one()
    {
        // "Sinks the unpriced" is a rule about the band it is in, not about the
        // whole page.
        var uow = Scene(
            directDepartsAt: DateTime.UtcNow.AddHours(1),
            passingDepartsAt: DateTime.UtcNow.AddHours(1),
            directPrice: null,
            passingPrice: 3m);

        AssertDirectLeads(await Search(uow, SearchSort.Price));
    }

    [Fact]
    public async Task Rating_and_seats_sorts_keep_the_bands_too()
    {
        var uow = Scene(
            directDepartsAt: DateTime.UtcNow.AddHours(1),
            passingDepartsAt: DateTime.UtcNow.AddHours(1));

        AssertDirectLeads(await Search(uow, SearchSort.Rating));
        AssertDirectLeads(await Search(uow, SearchSort.Seats));
        AssertDirectLeads(await Search(uow, SearchSort.Pickup));
        AssertDirectLeads(await Search(uow, SearchSort.Best));
    }
}
