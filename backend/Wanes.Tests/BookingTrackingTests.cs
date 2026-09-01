using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Trips;
using Wanes.Areas.Services.Users.Availability;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// The driver tracking each rider's seat, and the trip status that follows from
/// the seats. Both halves of one rule, so they are tested together.
/// </summary>
public class BookingTrackingTests
{
    private static (TripService svc, FakeUnitOfWork uow) Setup(int driverId = 2)
    {
        var uow = new FakeUnitOfWork();
        var svc = new TripService(uow, new FakeSecurityManager(driverId), new FakeAuditService(),
            new FakeNotificationService(), new DriverAvailabilityService(uow.Repository<Trip>()),
            uow.Repository<Trip>(), uow.Repository<TripStatusHistory>(), uow.Repository<Vehicle>(),
            uow.Repository<User>(), uow.Repository<Booking>(), uow.Repository<RideRequest>());
        return (svc, uow);
    }

    private static Booking Seat(int id, int tripId, int riderId, int seats = 1,
        BookingStatus status = BookingStatus.Confirmed) => new()
        {
            Id = id, TripId = tripId, RiderId = riderId, Seats = seats, Status = status,
        };

    /// <summary>A trip with two confirmed riders, seats already reserved.</summary>
    private static FakeUnitOfWork WithCarpool(FakeUnitOfWork uow, TripStatus status = TripStatus.Posted,
        int seatsTotal = 3)
    {
        var driver = Build.Driver(2);
        uow.Store<User>().Add(driver);
        uow.Store<User>().Add(Build.Rider(3));
        uow.Store<User>().Add(Build.Rider(4));
        var trip = Build.Trip(10, driverId: 2, vehicleId: 1, seatsTotal: seatsTotal, status: status);
        // The in-memory repository ignores Include shapers, so the navigation the
        // service reads the driver's trip count through is wired up by hand.
        trip.Driver = driver;
        trip.SeatsLeft = seatsTotal - 2;
        uow.Store<Trip>().Add(trip);
        uow.Store<Booking>().Add(Seat(1, 10, riderId: 3));
        uow.Store<Booking>().Add(Seat(2, 10, riderId: 4));
        return uow;
    }

    private static Trip Trip(FakeUnitOfWork uow) => uow.Store<Trip>().Single();
    private static Booking Seat(FakeUnitOfWork uow, int id) => uow.Store<Booking>().Single(b => b.Id == id);

    [Fact]
    public async Task Arriving_for_one_rider_moves_only_that_seat()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow);

        var res = await svc.SetBookingStatus(10, 1, BookingStatus.Arrived);

        Assert.True(res.Success);
        Assert.Equal(BookingStatus.Arrived, res.Data!.Status);
        Assert.Equal(BookingStatus.Confirmed, Seat(uow, 2).Status);
        Assert.Equal(TripStatus.Arrived, Trip(uow).Status);
    }

    [Fact]
    public async Task First_pickup_makes_the_trip_active()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow);

        var res = await svc.SetBookingStatus(10, 1, BookingStatus.InProgress);

        Assert.True(res.Success);
        Assert.Equal(TripStatus.Active, Trip(uow).Status);
        // The rider still waiting is untouched — one seat moved, not the manifest.
        Assert.Equal(BookingStatus.Confirmed, Seat(uow, 2).Status);
    }

    [Fact]
    public async Task Someone_aboard_outranks_someone_waiting()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow);

        await svc.SetBookingStatus(10, 1, BookingStatus.InProgress);
        // Reaching the second rider must not walk the trip back to Arrived while
        // the first is sitting in the car.
        await svc.SetBookingStatus(10, 2, BookingStatus.Arrived);

        Assert.Equal(TripStatus.Active, Trip(uow).Status);
    }

    [Fact]
    public async Task Last_dropoff_completes_the_trip_and_counts_it()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow);

        await svc.SetBookingStatus(10, 1, BookingStatus.InProgress);
        await svc.SetBookingStatus(10, 2, BookingStatus.InProgress);
        await svc.SetBookingStatus(10, 1, BookingStatus.Completed);

        // One rider still aboard — not finished yet.
        Assert.Equal(TripStatus.Active, Trip(uow).Status);

        await svc.SetBookingStatus(10, 2, BookingStatus.Completed);

        Assert.Equal(TripStatus.Completed, Trip(uow).Status);
        Assert.Equal(1, uow.Store<User>().Single(u => u.Id == 2).TripsAsDriver);
    }

    [Fact]
    public async Task A_no_show_does_not_hold_the_trip_open()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow);

        await svc.SetBookingStatus(10, 1, BookingStatus.InProgress);
        await svc.SetBookingStatus(10, 2, BookingStatus.NoShow);
        await svc.SetBookingStatus(10, 1, BookingStatus.Completed);

        Assert.Equal(TripStatus.Completed, Trip(uow).Status);
        Assert.Equal(BookingStatus.NoShow, Seat(uow, 2).Status);
    }

    [Fact]
    public async Task A_no_show_before_departure_frees_the_seat_again()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow, seatsTotal: 2);          // both seats taken -> Full
        Trip(uow).Status = TripStatus.Full;

        var res = await svc.SetBookingStatus(10, 2, BookingStatus.NoShow);

        Assert.True(res.Success);
        Assert.Equal(1, Trip(uow).SeatsLeft);
        Assert.Equal(TripStatus.Posted, Trip(uow).Status);
    }

    [Fact]
    public async Task A_no_show_under_way_keeps_the_seat_spent()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow);
        await svc.SetBookingStatus(10, 1, BookingStatus.InProgress);
        var before = Trip(uow).SeatsLeft;

        await svc.SetBookingStatus(10, 2, BookingStatus.NoShow);

        Assert.Equal(before, Trip(uow).SeatsLeft);
        Assert.Equal(TripStatus.Active, Trip(uow).Status);
    }

    [Fact]
    public async Task Dropping_off_a_rider_who_never_boarded_is_refused()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow);

        var res = await svc.SetBookingStatus(10, 1, BookingStatus.Completed);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.BookingStatusNotAllowed, res.ErrorCode);
        Assert.Equal(BookingStatus.Confirmed, Seat(uow, 1).Status);
        Assert.Equal(TripStatus.Posted, Trip(uow).Status);
    }

    [Fact]
    public async Task A_settled_seat_cannot_be_moved_again()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow);
        Seat(uow, 1).Status = BookingStatus.Cancelled;

        var res = await svc.SetBookingStatus(10, 1, BookingStatus.InProgress);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.BookingStatusNotAllowed, res.ErrorCode);
    }

    [Fact]
    public async Task Only_the_trips_own_driver_may_track_its_seats()
    {
        var (svc, uow) = Setup(driverId: 9);
        WithCarpool(uow);

        var res = await svc.SetBookingStatus(10, 1, BookingStatus.InProgress);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.TripNotFound, res.ErrorCode);
    }

    [Fact]
    public async Task A_seat_on_another_trip_is_not_found()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow);
        uow.Store<Booking>().Add(Seat(99, tripId: 77, riderId: 5));

        var res = await svc.SetBookingStatus(10, 99, BookingStatus.InProgress);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.BookingNotFound, res.ErrorCode);
    }

    // ── the trip-wide buttons, which are the same moves applied to everyone ──

    [Fact]
    public async Task Starting_the_trip_boards_every_rider()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow);
        Seat(uow, 2).Status = BookingStatus.Cancelled;

        var res = await svc.Start(10);

        Assert.True(res.Success);
        Assert.Equal(TripStatus.Active, Trip(uow).Status);
        Assert.Equal(BookingStatus.InProgress, Seat(uow, 1).Status);
        // A seat that was given back is not dragged along.
        Assert.Equal(BookingStatus.Cancelled, Seat(uow, 2).Status);
    }

    [Fact]
    public async Task Completing_the_trip_drops_everyone_still_aboard()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow);
        await svc.Start(10);

        var res = await svc.Complete(10);

        Assert.True(res.Success);
        Assert.Equal(TripStatus.Completed, Trip(uow).Status);
        Assert.All(uow.Store<Booking>(), b => Assert.Equal(BookingStatus.Completed, b.Status));
    }

    [Fact]
    public async Task A_driver_left_with_no_riders_can_still_finish_the_trip()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow);
        // Everyone bailed after the driver set off.
        await svc.Start(10);
        Seat(uow, 1).Status = BookingStatus.Cancelled;
        Seat(uow, 2).Status = BookingStatus.Cancelled;

        var res = await svc.Complete(10);

        Assert.True(res.Success);
        Assert.Equal(TripStatus.Completed, Trip(uow).Status);
    }

    [Fact]
    public async Task A_finished_trip_takes_no_further_seat_moves()
    {
        var (svc, uow) = Setup();
        WithCarpool(uow);
        await svc.Start(10);
        await svc.Complete(10);

        var res = await svc.SetBookingStatus(10, 1, BookingStatus.NoShow);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.Conflict, res.ErrorCode);
    }
}
