using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Trips;
using Wanes.Areas.Services.Trips.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

public class TripServiceTests
{
    private static (TripService svc, FakeUnitOfWork uow) Setup(int driverId = 2)
    {
        var uow = new FakeUnitOfWork();
        var svc = new TripService(uow, new FakeSecurityManager(driverId), new FakeAuditService(),
            new FakeNotificationService(),
            uow.Repository<Trip>(), uow.Repository<TripStatusHistory>(), uow.Repository<Vehicle>(),
            uow.Repository<User>(), uow.Repository<Booking>(), uow.Repository<RideRequest>());
        return (svc, uow);
    }

    private static CreateTripInput Input(int vehicleId = 1, int seats = 2, int minutesAhead = 30) => new()
    {
        VehicleId = vehicleId,
        Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
        Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
        DepartAt = DateTime.UtcNow.AddMinutes(minutesAhead),
        SeatsTotal = seats,
    };

    private static UpdateTripInput UpdateInput(int vehicleId = 1, int seats = 2, int minutesAhead = 90) => new()
    {
        VehicleId = vehicleId,
        Origin = new GeoPoint { Lat = 31.90, Lng = 35.90, Address = "A2" },
        Destination = new GeoPoint { Lat = 32.05, Lng = 35.80, Address = "B2" },
        DepartAt = DateTime.UtcNow.AddMinutes(minutesAhead),
        SeatsTotal = seats,
    };

    [Fact]
    public async Task Create_publishes_without_admin_verification()
    {
        var (svc, uow) = Setup(driverId: 2);
        uow.Store<User>().Add(Build.Driver(2, DriverStatus.Pending));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: 2));

        var res = await svc.Create(Input());

        Assert.True(res.Success);
        Assert.Equal(TripStatus.Posted, res.Data!.Status);
    }

    [Fact]
    public async Task Create_fails_when_departure_in_past()
    {
        var (svc, uow) = Setup(driverId: 2);
        uow.Store<User>().Add(Build.Driver(2));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: 2));

        var res = await svc.Create(Input(minutesAhead: -5));

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.DepartureMustBeFuture, res.ErrorCode);
    }

    [Fact]
    public async Task Create_fails_when_seats_exceed_capacity()
    {
        var (svc, uow) = Setup(driverId: 2);
        uow.Store<User>().Add(Build.Driver(2));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: 2, capacity: 3));

        var res = await svc.Create(Input(seats: 5));

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.SeatsExceedCapacity, res.ErrorCode);
    }

    [Fact]
    public async Task Create_succeeds_and_sets_seats_left()
    {
        var (svc, uow) = Setup(driverId: 2);
        uow.Store<User>().Add(Build.Driver(2));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: 2, capacity: 4));

        var res = await svc.Create(Input(seats: 2));

        Assert.True(res.Success);
        Assert.Equal(2, res.Data!.SeatsLeft);
        Assert.Equal(TripStatus.Posted, res.Data!.Status);
    }

    [Fact]
    public async Task Update_changes_a_posted_trip_with_no_bookings()
    {
        var (svc, uow) = Setup(driverId: 2);
        uow.Store<User>().Add(Build.Driver(2));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: 2, capacity: 4));
        uow.Store<Trip>().Add(Build.Trip(7, driverId: 2, vehicleId: 1, seatsTotal: 3));

        var res = await svc.Update(7, UpdateInput(seats: 2));

        Assert.True(res.Success);
        Assert.Equal("A2", res.Data!.OriginAddress);
        Assert.Equal(2, res.Data!.SeatsTotal);
        Assert.Equal(2, res.Data!.SeatsLeft);
        Assert.Equal(TripStatus.Posted, res.Data!.Status);
    }

    [Fact]
    public async Task Update_fails_once_the_trip_has_a_booking()
    {
        var (svc, uow) = Setup(driverId: 2);
        uow.Store<User>().Add(Build.Driver(2));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: 2, capacity: 4));
        uow.Store<Trip>().Add(Build.Trip(7, driverId: 2, vehicleId: 1));
        uow.Store<Booking>().Add(new Booking
        {
            Id = 1, TripId = 7, RiderId = 3, Seats = 1, Status = BookingStatus.Confirmed,
        });

        var res = await svc.Update(7, UpdateInput());

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.TripNotEditable, res.ErrorCode);
    }

    [Fact]
    public async Task Update_fails_when_trip_is_not_posted()
    {
        var (svc, uow) = Setup(driverId: 2);
        uow.Store<User>().Add(Build.Driver(2));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: 2, capacity: 4));
        uow.Store<Trip>().Add(Build.Trip(7, driverId: 2, vehicleId: 1, status: TripStatus.Active));

        var res = await svc.Update(7, UpdateInput());

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.TripNotEditable, res.ErrorCode);
    }

    [Fact]
    public async Task Update_fails_for_another_drivers_trip()
    {
        var (svc, uow) = Setup(driverId: 2);
        uow.Store<User>().Add(Build.Driver(2));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: 2, capacity: 4));
        uow.Store<Trip>().Add(Build.Trip(7, driverId: 9, vehicleId: 1));

        var res = await svc.Update(7, UpdateInput());

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.TripNotFound, res.ErrorCode);
    }
}
