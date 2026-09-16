using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Bookings.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// "Only if it is worth it": a driver's seat threshold, and the clock that
/// forces an answer when it is not met.
///
/// The shape to protect is that nobody is committed early and nobody is
/// stranded late. Seats held on a gathering trip are Pending — real holds, no
/// commitment — the seat that meets the threshold commits everybody at once,
/// and a trip still short at its cutoff is called off in time for its riders to
/// find another ride rather than left to fail at the kerb.
/// </summary>
public class SeatThresholdTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;
    private const int OtherRiderId = 6;

    /// <summary>A four-seat trip that only runs if <paramref name="minSeats"/> are taken.</summary>
    private static FakeUnitOfWork Scene(int minSeats = 3, int hoursAhead = 6)
    {
        var uow = new FakeUnitOfWork();
        var driver = Build.Driver(DriverId);
        uow.Store<User>().Add(driver);
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<User>().Add(Build.Rider(OtherRiderId));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));

        var trip = Build.Trip(10, driverId: DriverId, vehicleId: 1, seatsTotal: 4);
        trip.Driver = driver;
        trip.DepartAt = DateTime.UtcNow.AddHours(hoursAhead);
        trip.MinSeatsToConfirm = minSeats;
        uow.Store<Trip>().Add(trip);
        return uow;
    }

    private static async Task<BaseResponse<BookingOutput>> Book(FakeUnitOfWork uow, int riderId,
        int seats = 1, FakeNotificationService? notifications = null) =>
        await Make.Bookings(uow, riderId, notifications)
            .Create(new CreateBookingInput { TripId = 10, Seats = seats });

    // ── Gathering ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_seat_on_a_trip_short_of_its_threshold_is_held_not_committed()
    {
        var uow = Scene(minSeats: 3);

        var res = await Book(uow, RiderId);

        Assert.True(res.Success);
        Assert.Equal(BookingStatus.Pending, res.Data!.Status);

        // Held all the same: it is off the trip's count, so nobody else can be
        // sold it.
        Assert.Equal(3, uow.Store<Trip>()[0].SeatsLeft);
    }

    [Fact]
    public async Task A_held_seat_is_the_only_kind_of_pending_seat_left()
    {
        var uow = Scene(minSeats: 3);

        await Book(uow, RiderId);

        // On a trip somebody is driving, Pending means one thing: short of the
        // threshold. Its clock is the trip's own confirm cutoff, so the seat
        // carries no deadline of its own and nothing else can resolve it.
        var seat = uow.Store<Booking>()[0];
        Assert.True(BookingStatusRules.IsPending(seat.Status));
        Assert.NotNull(uow.Store<Trip>().Single(t => t.Id == seat.TripId).DriverId);
    }

    [Fact]
    public async Task The_seat_that_meets_the_threshold_commits_everybody()
    {
        var uow = Scene(minSeats: 3);
        var notifications = new FakeNotificationService();
        await Book(uow, RiderId, seats: 2);

        var res = await Book(uow, OtherRiderId, notifications: notifications);

        Assert.True(res.Success);
        Assert.All(uow.Store<Booking>(), b => Assert.Equal(BookingStatus.Confirmed, b.Status));

        // The rider who had been waiting is told; the one who just booked has
        // their own confirmation already.
        Assert.Contains($"{RiderId}:{NotificationTemplate.TripConfirmedRider}", notifications.Sent);
    }

    [Fact]
    public async Task A_trip_with_no_threshold_confirms_the_first_seat()
    {
        var uow = Scene(minSeats: 1);

        var res = await Book(uow, RiderId);

        Assert.Equal(BookingStatus.Confirmed, res.Data!.Status);
    }

    [Fact]
    public async Task The_driver_cannot_pick_up_a_rider_who_is_only_holding_a_seat()
    {
        var uow = Scene(minSeats: 3);
        await Book(uow, RiderId);
        var bookingId = uow.Store<Booking>()[0].Id;

        var res = await Make.Trips(uow, DriverId)
            .SetBookingStatus(10, bookingId, BookingStatus.InProgress);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.BookingStatusNotAllowed, res.ErrorCode);
    }

    // ── The driver's decision ────────────────────────────────────────────────

    [Fact]
    public async Task The_driver_may_run_with_the_seats_they_have()
    {
        var uow = Scene(minSeats: 3);
        var notifications = new FakeNotificationService();
        await Book(uow, RiderId);

        var res = await Make.Confirmations(uow, DriverId, notifications).ConfirmNow(10);

        Assert.True(res.Success);
        Assert.Equal(BookingStatus.Confirmed, uow.Store<Booking>()[0].Status);
        Assert.Contains($"{RiderId}:{NotificationTemplate.TripConfirmedRider}", notifications.Sent);

        // Nothing should ask the driver about this trip again. The stamp is what
        // says so, and the threshold stays as posted — it is the record of what
        // they asked for, and rewriting it would lose the fact that they ran
        // short-handed.
        var trip = uow.Store<Trip>()[0];
        Assert.True(trip.IsConfirmed);
        Assert.Equal(3, trip.MinSeatsToConfirm);
        Assert.False(TripConfirmationRules.IsGathering(
            trip.Status, trip.MinSeatsToConfirm, heldSeats: 1, trip.ConfirmedAt));
    }

    [Fact]
    public async Task The_driver_may_call_it_off_and_the_riders_are_told_why()
    {
        var uow = Scene(minSeats: 3);
        var notifications = new FakeNotificationService();
        await Book(uow, RiderId);

        var res = await Make.Confirmations(uow, DriverId, notifications).CancelForLowSeats(10);

        Assert.True(res.Success);
        Assert.Equal(TripStatus.Cancelled, uow.Store<Trip>()[0].Status);
        Assert.Equal(BookingStatus.Cancelled, uow.Store<Booking>()[0].Status);

        // Not "the driver cancelled": that reads as a choice made about you, and
        // this one was made about the empty seats.
        Assert.Contains($"{RiderId}:{NotificationTemplate.TripLowSeatsCancelledRider}",
            notifications.Sent);
    }

    [Fact]
    public async Task A_trip_that_already_met_its_threshold_has_nothing_to_decide()
    {
        var uow = Scene(minSeats: 1);
        await Book(uow, RiderId);

        var res = await Make.Confirmations(uow, DriverId).ConfirmNow(10);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.TripNotConfirmable, res.ErrorCode);
    }

    // ── The clock ────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_driver_is_asked_once_when_the_decision_comes_due()
    {
        // Default windows: the cutoff is an hour before departure and the prompt
        // a quarter of an hour before that, so a trip 70 minutes out is inside
        // the window.
        var uow = Scene(minSeats: 3);
        uow.Store<Trip>()[0].DepartAt = DateTime.UtcNow.AddMinutes(70);
        await Book(uow, RiderId);

        var notifications = new FakeNotificationService();
        var confirmations = Make.Confirmations(uow, DriverId, notifications);

        var first = await confirmations.PromptDue();
        var second = await confirmations.PromptDue();

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Single(notifications.Sent,
            s => s == $"{DriverId}:{NotificationTemplate.TripConfirmDecisionDriver}");
        Assert.NotNull(uow.Store<Trip>()[0].ConfirmPromptedAt);
    }

    [Fact]
    public async Task A_trip_still_hours_out_is_not_asked_about_yet()
    {
        var uow = Scene(minSeats: 3, hoursAhead: 8);
        await Book(uow, RiderId);

        Assert.Equal(0, await Make.Confirmations(uow, DriverId).PromptDue());
    }

    [Fact]
    public async Task Silence_at_the_cutoff_calls_the_trip_off()
    {
        var uow = Scene(minSeats: 3);
        uow.Store<Trip>()[0].DepartAt = DateTime.UtcNow.AddMinutes(30);
        await Book(uow, RiderId);
        var notifications = new FakeNotificationService();

        var resolved = await Make.Confirmations(uow, DriverId, notifications).ResolveDue();

        Assert.Equal(1, resolved);
        Assert.Equal(TripStatus.Cancelled, uow.Store<Trip>()[0].Status);
        Assert.Equal(BookingStatus.Cancelled, uow.Store<Booking>()[0].Status);
        Assert.Contains($"{RiderId}:{NotificationTemplate.TripLowSeatsCancelledRider}",
            notifications.Sent);
    }

    [Fact]
    public async Task A_trip_that_filled_up_is_left_alone_by_the_sweep()
    {
        var uow = Scene(minSeats: 2);
        uow.Store<Trip>()[0].DepartAt = DateTime.UtcNow.AddMinutes(30);
        await Book(uow, RiderId, seats: 2);

        Assert.Equal(0, await Make.Confirmations(uow, DriverId).ResolveDue());
        Assert.Equal(TripStatus.Posted, uow.Store<Trip>()[0].Status);
    }

    // ── The rule itself ──────────────────────────────────────────────────────

    [Fact]
    public void Held_seats_count_the_uncommitted_ones_too()
    {
        var bookings = new List<Booking>
        {
            new() { Seats = 2, Status = BookingStatus.Pending },
            new() { Seats = 1, Status = BookingStatus.Confirmed },
            new() { Seats = 3, Status = BookingStatus.Cancelled },
        };

        // A rider who has taken a seat and not yet answered the price is as much
        // a passenger-in-waiting as one who has; excluding them would leave a
        // trip permanently one seat short of confirming itself.
        Assert.Equal(3, TripConfirmationRules.HeldSeats(bookings));
    }

    [Fact]
    public void A_threshold_is_never_more_than_the_trip_can_seat()
    {
        Assert.Equal(4, TripConfirmationRules.ThresholdFor(9, seatsTotal: 4));
        Assert.Equal(TripConfirmationRules.NoThreshold, TripConfirmationRules.ThresholdFor(0, 4));
    }

    [Fact]
    public void The_prompt_comes_before_the_deadline()
    {
        var departAt = new DateTime(2026, 9, 8, 18, 0, 0, DateTimeKind.Utc);

        var deadline = TripConfirmationRules.DeadlineFor(departAt, 60);
        var prompt = TripConfirmationRules.PromptAtFor(departAt, 60, 15);

        Assert.Equal(departAt.AddMinutes(-60), deadline);
        Assert.Equal(deadline.AddMinutes(-15), prompt);
    }
}
