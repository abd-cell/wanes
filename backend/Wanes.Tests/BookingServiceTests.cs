using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
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
        uow.Store<User>().Add(Build.Rider(riderId));
        var svc = Make.Bookings(uow, riderId);
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
    public async Task Booking_the_same_seats_twice_is_the_same_booking()
    {
        var (svc, uow, _) = Setup(riderId: 1);
        uow.Store<Trip>().Add(Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 3));

        var first = await svc.Create(new CreateBookingInput { TripId = 10, Seats = 1 });
        var second = await svc.Create(new CreateBookingInput { TripId = 10, Seats = 1 });

        // A double tap, or a client re-sending a request whose answer it never
        // saw. Refusing it would tell a rider they have no seat while they are
        // holding one — and the seat must not be taken twice either.
        Assert.True(second.Success);
        Assert.Equal(first.Data!.Id, second.Data!.Id);
        Assert.Single(uow.Store<Booking>());
        Assert.Equal(2, uow.Store<Trip>()[0].SeatsLeft);
    }

    [Fact]
    public async Task Booking_different_seats_on_a_trip_they_are_on_is_refused()
    {
        var (svc, uow, _) = Setup(riderId: 1);
        uow.Store<Trip>().Add(Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 3));

        await svc.Create(new CreateBookingInput { TripId = 10, Seats = 1 });
        var second = await svc.Create(new CreateBookingInput { TripId = 10, Seats = 2 });

        // Not a retry — a second intention. Changing a booking is a different
        // operation from making one, and silently adding a seat is neither.
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

    [Fact]
    public async Task Cancelling_twice_succeeds_and_returns_the_seats_once()
    {
        var (svc, uow, _) = Setup(riderId: 1);
        uow.Store<Trip>().Add(Build.Trip(id: 10, driverId: 2, vehicleId: 1, seatsTotal: 3));

        var booked = await svc.Create(new CreateBookingInput { TripId = 10, Seats = 2 });
        await svc.Cancel(booked.Data!.Id);
        var again = await svc.Cancel(booked.Data.Id);

        // The rider asked for the seat to be gone and it is gone. A retry of a
        // cancel that worked must not read as a failure — and must not hand the
        // trip its seats a second time.
        Assert.True(again.Success);
        Assert.Equal(3, uow.Store<Trip>().Single().SeatsLeft);
    }
}
