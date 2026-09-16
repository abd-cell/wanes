using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.RideRequests.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Notifications;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// The seats a claim produces.
///
/// A claimed posting becomes a trip the driver owns, and its riders are on it
/// the way anybody is on a driver's trip: confirmed, with the driver's number
/// and the driver's price. There is no second handshake — the riders asked for
/// this ride, and the one thing they had not agreed to is the figure, which
/// they answer by staying or by leaving.
///
/// That is the whole shape worth pinning: no pending seat, no deadline, and
/// leaving works exactly as it does on any other trip.
/// </summary>
public class ClaimedSeatTests
{
    private const int RiderId = 5;
    private const int JoinerId = 6;
    private const int DriverId = 2;

    /// <summary>A claimed posting: two riders aboard, priced, driving.</summary>
    private static async Task<FakeUnitOfWork> Claimed(
        FakeNotificationService? notifications = null, bool withJoiner = false)
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<User>().Add(Build.Rider(JoinerId));

        var driver = Build.Driver(DriverId);
        driver.IsOnline = true;
        driver.LastLocation = GeoFactory.Point(31.95, 35.92);
        uow.Store<User>().Add(driver);
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));

        Build.Demand(uow, 30, RiderId, departAt: DateTime.UtcNow.AddHours(4));
        if (withJoiner) await Make.Requests(uow, JoinerId).Join(30, new JoinRideRequestInput());

        await Make.Interests(uow, DriverId, notifications)
            .ExpressInterest(30, new ExpressInterestInput { PricePerSeat = 4m });
        return uow;
    }

    [Fact]
    public async Task Formation_confirms_the_seats_outright()
    {
        var uow = await Claimed(withJoiner: true);

        // The seats are on the trip the request became — a new row, and the one
        // place in the system where an id changes. The request's MatchedTripId
        // is the thread across it, and it is what everything downstream follows.
        var matchedTripId = uow.Store<RideRequest>().Single().MatchedTripId;
        Assert.NotNull(matchedTripId);

        var bookings = uow.Store<Booking>();
        Assert.Equal(2, bookings.Count);
        Assert.All(bookings, b =>
        {
            // Not pending, and nothing to answer: this is the same seat a rider
            // gets by booking a trip the driver published themselves.
            Assert.Equal(BookingStatus.Confirmed, b.Status);
            Assert.Equal(matchedTripId, b.TripId);
        });
        Assert.Equal([RiderId, JoinerId], bookings.Select(b => b.RiderId).Order().ToList());
    }

    [Fact]
    public async Task The_seat_carries_the_price_and_the_drivers_number_at_once()
    {
        var uow = await Claimed();
        uow.Store<Booking>()[0].Trip = uow.Store<Trip>()[0];
        uow.Store<Booking>()[0].Trip!.Driver = uow.Store<User>().First(u => u.Id == DriverId);

        var mine = await Make.Bookings(uow, RiderId).GetUserBookings();

        var row = Assert.Single(mine.Data!);
        Assert.Equal(4m, row.PricePerSeat);
        Assert.NotNull(row.DriverPhone);
    }

    [Fact]
    public async Task The_riders_are_told_a_driver_took_it()
    {
        var notifications = new FakeNotificationService();
        await Claimed(notifications);

        Assert.Contains($"{RiderId}:{NotificationTemplate.RideRequestMatchedRider}", notifications.Sent);
    }

    [Fact]
    public async Task A_rider_who_does_not_like_the_price_leaves()
    {
        var uow = await Claimed();
        var seat = uow.Store<Booking>()[0];
        var seatsBefore = uow.Store<Trip>()[0].SeatsLeft;
        var notifications = new FakeNotificationService();

        var res = await Make.Bookings(uow, RiderId, notifications).Cancel(seat.Id);

        Assert.True(res.Success);
        Assert.Equal(BookingStatus.Cancelled, uow.Store<Booking>()[0].Status);
        Assert.Equal(seatsBefore + 1, uow.Store<Trip>()[0].SeatsLeft);

        // The driver hears it as what it is — a seat came back — because that is
        // all it is. Leaving is free, and there is nothing to charge.
        Assert.Contains($"{DriverId}:{NotificationTemplate.BookingCancelledDriver}", notifications.Sent);
    }

    [Fact]
    public async Task Every_rider_leaving_leaves_the_trip_standing_with_a_full_car()
    {
        var uow = await Claimed();
        var seat = uow.Store<Booking>()[0];

        await Make.Bookings(uow, RiderId).Cancel(seat.Id);

        // The driver has a real route and a real price; deleting the trip would
        // punish them for the riders' choice.
        var trip = uow.Store<Trip>()[0];
        Assert.Equal(TripStatus.Posted, trip.Status);
        Assert.Equal(trip.SeatsTotal, trip.SeatsLeft);
    }

    [Fact]
    public async Task The_driver_can_track_a_claimed_seat_straight_away()
    {
        // A pending seat is the one a driver cannot move. A claimed seat is not
        // pending, so the trip can actually run.
        var uow = await Claimed();
        var seat = uow.Store<Booking>()[0];

        var res = await Make.Trips(uow, DriverId)
            .SetBookingStatus(uow.Store<Trip>()[0].Id, seat.Id, BookingStatus.Arrived);

        Assert.True(res.Success);
        Assert.Equal(BookingStatus.Arrived, uow.Store<Booking>()[0].Status);
    }
}
