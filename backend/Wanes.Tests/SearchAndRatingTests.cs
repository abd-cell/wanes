using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Ratings;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Ratings;
using Wanes.Areas.Services.Ratings.Models;
using Wanes.Areas.Services.Search;
using Wanes.Areas.Services.Search.Models;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

public class SearchServiceTests
{
    private static SearchInput Input() => new()
    {
        Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
        Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
        When = DateTime.UtcNow.AddMinutes(20),
        Seats = 1,
    };

    [Fact]
    public async Task No_match_opens_a_hail_request()
    {
        var uow = new FakeUnitOfWork();
        var notifications = new FakeNotificationService { NearbyDriverCount = 3 };
        var svc = new SearchService(uow, new FakeSecurityManager(1), new FakeAuditService(), notifications,
            new FakeAppConfigurationService(),
            uow.Repository<Trip>(), uow.Repository<User>(), uow.Repository<Vehicle>(), uow.Repository<RideRequest>(),
            uow.Repository<Booking>());

        var res = await svc.Search(Input());

        Assert.True(res.Success);
        Assert.Equal(SearchMode.Hail, res.Data!.Mode);
        Assert.NotNull(res.Data.RideRequestId);
        Assert.Equal(3, res.Data.DriversNotified);
        Assert.Single(uow.Store<RideRequest>());
    }

    [Fact]
    public async Task Matching_trip_returns_carpool()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(2));
        uow.Store<Trip>().Add(Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 3));
        var svc = new SearchService(uow, new FakeSecurityManager(1), new FakeAuditService(),
            new FakeNotificationService(), new FakeAppConfigurationService(),
            uow.Repository<Trip>(), uow.Repository<User>(), uow.Repository<Vehicle>(), uow.Repository<RideRequest>(),
            uow.Repository<Booking>());

        var res = await svc.Search(Input());

        Assert.True(res.Success);
        Assert.Equal(SearchMode.Carpool, res.Data!.Mode);
        Assert.Single(res.Data.Matches);
        Assert.Empty(uow.Store<RideRequest>());
    }

    [Fact]
    public async Task Trip_long_past_its_departure_is_not_offered()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(2));
        var trip = Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 3);
        // Well past the boarding grace: gone, and the driver simply never
        // pressed start. The search window still reaches back this far.
        trip.DepartAt = DateTime.UtcNow - MatchRules.BoardingGrace - TimeSpan.FromMinutes(5);
        uow.Store<Trip>().Add(trip);

        var input = Input();
        input.When = DateTime.UtcNow;                     // window reaches 30 min back

        var res = await Service(uow).Search(input);

        Assert.True(res.Success);
        Assert.Equal(SearchMode.Hail, res.Data!.Mode);
    }

    [Fact]
    public async Task Trip_a_few_minutes_late_is_still_offered()
    {
        // Inside the boarding grace: the driver is running late, not gone. The
        // rider is standing at the pickup and can still take the seat — and this
        // is the same window that keeps a hail-accepted trip joinable once its
        // pickup lead has elapsed.
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(2));
        var trip = Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 3);
        trip.DepartAt = DateTime.UtcNow.AddMinutes(-3);
        uow.Store<Trip>().Add(trip);

        var input = Input();
        input.When = DateTime.UtcNow;

        var res = await Service(uow).Search(input);

        Assert.True(res.Success);
        Assert.Equal(SearchMode.Carpool, res.Data!.Mode);
        Assert.Single(res.Data.Matches);
    }

    [Fact]
    public async Task Trip_the_rider_already_booked_is_not_offered()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(2));
        uow.Store<Trip>().Add(Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 3));
        uow.Store<Booking>().Add(new Booking
        {
            Id = 1, TripId = 10, RiderId = 1, Seats = 1, Status = BookingStatus.Confirmed,
        });

        var res = await Service(uow).Search(Input());

        Assert.True(res.Success);
        Assert.Equal(SearchMode.Hail, res.Data!.Mode);
    }

    [Fact]
    public async Task Cancelled_booking_frees_the_trip_to_show_again()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(2));
        uow.Store<Trip>().Add(Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 3));
        uow.Store<Booking>().Add(new Booking
        {
            Id = 1, TripId = 10, RiderId = 1, Seats = 1, Status = BookingStatus.Cancelled,
        });

        var res = await Service(uow).Search(Input());

        Assert.True(res.Success);
        Assert.Equal(SearchMode.Carpool, res.Data!.Mode);
        Assert.Single(res.Data.Matches);
    }

    [Fact]
    public async Task Trip_from_a_disabled_driver_is_not_offered()
    {
        var uow = new FakeUnitOfWork();
        var driver = Build.Driver(2);
        driver.IsDisabled = true;
        uow.Store<User>().Add(driver);
        var trip = Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 3);
        trip.Driver = driver;
        uow.Store<Trip>().Add(trip);

        var res = await Service(uow).Search(Input());

        Assert.True(res.Success);
        Assert.Equal(SearchMode.Hail, res.Data!.Mode);
    }

    [Fact]
    public async Task Departure_sort_puts_the_soonest_first()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(2));
        foreach (var (id, minutes) in new[] { (10, 40), (11, 15), (12, 25) })
        {
            var trip = Build.Trip(id: id, driverId: 2, vehicleId: 1, seatsTotal: 3);
            trip.DepartAt = DateTime.UtcNow.AddMinutes(minutes);
            uow.Store<Trip>().Add(trip);
        }

        var input = Input();
        input.SortBy = SearchSort.Departure;

        var res = await Service(uow).Search(input);

        Assert.True(res.Success);
        Assert.Equal([11, 12, 10], res.Data!.Matches.Select(m => m.Id));
    }

    [Fact]
    public async Task Price_sort_puts_the_cheapest_first_and_the_unpriced_last()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(2));
        foreach (var (id, price) in new (int, decimal?)[] { (10, 5m), (11, null), (12, 2m) })
        {
            var trip = Build.Trip(id: id, driverId: 2, vehicleId: 1, seatsTotal: 3);
            trip.PricePerSeat = price;
            uow.Store<Trip>().Add(trip);
        }

        var input = Input();
        input.SortBy = SearchSort.Price;

        var res = await Service(uow).Search(input);

        Assert.True(res.Success);
        // A trip with no price set is not the cheapest one -- it sinks.
        Assert.Equal([12, 10, 11], res.Data!.Matches.Select(m => m.Id));
    }

    [Fact]
    public async Task Seats_sort_puts_the_roomiest_first()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(2));
        foreach (var (id, seats) in new[] { (10, 2), (11, 4), (12, 3) })
        {
            var trip = Build.Trip(id: id, driverId: 2, vehicleId: 1, seatsTotal: seats);
            uow.Store<Trip>().Add(trip);
        }

        var input = Input();
        input.SortBy = SearchSort.Seats;

        var res = await Service(uow).Search(input);

        Assert.True(res.Success);
        Assert.Equal([11, 12, 10], res.Data!.Matches.Select(m => m.Id));
    }

    [Fact]
    public async Task Unsorted_search_keeps_the_default_proximity_ranking()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(2));
        // Departure and price both disagree with proximity here, so a match on
        // the arrival order means neither leaked into the default.
        foreach (var (id, minutes, price) in new (int, int, decimal?)[] { (10, 40, 9m), (11, 15, 1m) })
        {
            var trip = Build.Trip(id: id, driverId: 2, vehicleId: 1, seatsTotal: 3);
            trip.DepartAt = DateTime.UtcNow.AddMinutes(minutes);
            trip.PricePerSeat = price;
            uow.Store<Trip>().Add(trip);
        }

        var res = await Service(uow).Search(Input());

        Assert.True(res.Success);
        Assert.Equal(2, res.Data!.Matches.Count);
    }

    private static SearchService Service(FakeUnitOfWork uow, INotificationService? notifications = null) =>
        new(uow, new FakeSecurityManager(1), new FakeAuditService(),
            notifications ?? new FakeNotificationService(), new FakeAppConfigurationService(),
            uow.Repository<Trip>(), uow.Repository<User>(), uow.Repository<Vehicle>(), uow.Repository<RideRequest>(),
            uow.Repository<Booking>());
}

public class RatingServiceTests
{
    [Fact]
    public async Task Rating_before_completion_is_rejected()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<Trip>().Add(Build.Trip(id: 10, driverId: 2, vehicleId: 1));
        uow.Store<Booking>().Add(new Booking { Id = 5, TripId = 10, RiderId = 1, Status = BookingStatus.Confirmed });
        var svc = new RatingService(uow, new FakeSecurityManager(1), new FakeAuditService(),
            new FakeNotificationService(),
            uow.Repository<Booking>(), uow.Repository<Trip>(), uow.Repository<Rating>(), uow.Repository<User>());

        var res = await svc.Rate(new CreateRatingInput { BookingId = 5, Stars = 5 });

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.RatingNotAllowed, res.ErrorCode);
    }

    [Fact]
    public async Task Rating_updates_target_average()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(2)); // target
        uow.Store<Trip>().Add(Build.Trip(id: 10, driverId: 2, vehicleId: 1));
        uow.Store<Booking>().Add(new Booking { Id = 5, TripId = 10, RiderId = 1, Status = BookingStatus.Completed });
        var svc = new RatingService(uow, new FakeSecurityManager(1), new FakeAuditService(),
            new FakeNotificationService(),
            uow.Repository<Booking>(), uow.Repository<Trip>(), uow.Repository<Rating>(), uow.Repository<User>());

        var res = await svc.Rate(new CreateRatingInput { BookingId = 5, Stars = 4 });

        Assert.True(res.Success);
        var driver = uow.Store<User>().Single(u => u.Id == 2);
        Assert.Equal(1, driver.RatingCount);
        Assert.Equal(4, driver.RatingAvg);
        Assert.Single(uow.Store<Rating>());
    }
}
