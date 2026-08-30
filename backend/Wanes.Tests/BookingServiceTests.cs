using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Services.Bookings;
using Wanes.Areas.Services.Bookings.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

public class BookingServiceTests
{
    private static (BookingService svc, FakeUnitOfWork uow, FakeSecurityManager sec) Setup(int riderId = 1)
    {
        var uow = new FakeUnitOfWork();
        var sec = new FakeSecurityManager(riderId);
        var svc = new BookingService(uow, sec, new FakeAuditService(),
            uow.Repository<Booking>(), uow.Repository<Trip>());
        return (svc, uow, sec);
    }

    [Fact]
    public async Task Book_confirms_and_decrements_seats()
    {
        var (svc, uow, _) = Setup(riderId: 1);
        uow.Store<Trip>().Add(Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 3));

        var res = await svc.Create(new CreateBookingInput { TripId = 10, Seats = 1 });

        Assert.True(res.Success);
        Assert.Equal(BookingStatus.Confirmed, res.Data!.Status);
        Assert.Equal(2, uow.Store<Trip>().Single().SeatsLeft);
    }

    [Fact]
    public async Task Cannot_book_own_trip()
    {
        var (svc, uow, _) = Setup(riderId: 2); // same as driver
        uow.Store<Trip>().Add(Build.Trip(id: 10, driverId: 2, vehicleId: 1));

        var res = await svc.Create(new CreateBookingInput { TripId = 10, Seats = 1 });

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.CannotBookOwnTrip, res.ErrorCode);
    }

    [Fact]
    public async Task Cannot_double_book()
    {
        var (svc, uow, _) = Setup(riderId: 1);
        uow.Store<Trip>().Add(Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 3));

        await svc.Create(new CreateBookingInput { TripId = 10, Seats = 1 });
        var second = await svc.Create(new CreateBookingInput { TripId = 10, Seats = 1 });

        Assert.False(second.Success);
        Assert.Equal(ErrorCode.AlreadyBooked, second.ErrorCode);
    }

    [Fact]
    public async Task No_seats_left_fails()
    {
        var (svc, uow, _) = Setup(riderId: 1);
        uow.Store<Trip>().Add(Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 1));

        var res = await svc.Create(new CreateBookingInput { TripId = 10, Seats = 2 });

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.NoSeatsLeft, res.ErrorCode);
    }

    [Fact]
    public async Task Cancel_returns_seats_to_trip()
    {
        var (svc, uow, _) = Setup(riderId: 1);
        uow.Store<Trip>().Add(Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 3));

        var booked = await svc.Create(new CreateBookingInput { TripId = 10, Seats = 2 });
        Assert.Equal(1, uow.Store<Trip>().Single().SeatsLeft);

        var cancel = await svc.Cancel(booked.Data!.Id);

        Assert.True(cancel.Success);
        Assert.Equal(3, uow.Store<Trip>().Single().SeatsLeft);
        Assert.Equal(BookingStatus.Cancelled, uow.Store<Booking>().Single().Status);
    }
}
