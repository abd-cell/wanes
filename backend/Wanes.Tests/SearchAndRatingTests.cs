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
            new FakeNotificationService(),
            uow.Repository<Trip>(), uow.Repository<User>(), uow.Repository<Vehicle>(), uow.Repository<RideRequest>(),
            uow.Repository<Booking>());

        var res = await svc.Search(Input());

        Assert.True(res.Success);
        Assert.Equal(SearchMode.Carpool, res.Data!.Mode);
        Assert.Single(res.Data.Matches);
        Assert.Empty(uow.Store<RideRequest>());
    }

    [Fact]
    public async Task Trip_that_already_departed_is_not_offered()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(2));
        var trip = Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 3);
        trip.DepartAt = DateTime.UtcNow.AddMinutes(-5);   // left already, driver never pressed start
        uow.Store<Trip>().Add(trip);

        var input = Input();
        input.When = DateTime.UtcNow;                     // window reaches 30 min back

        var res = await Service(uow).Search(input);

        Assert.True(res.Success);
        Assert.Equal(SearchMode.Hail, res.Data!.Mode);
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

    private static SearchService Service(FakeUnitOfWork uow, INotificationService? notifications = null) =>
        new(uow, new FakeSecurityManager(1), new FakeAuditService(),
            notifications ?? new FakeNotificationService(),
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
