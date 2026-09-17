using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Bookings;
using Wanes.Areas.Services.Bookings.Models;
using Wanes.Areas.Services.RideRequests;
using Wanes.Areas.Services.Users.Availability;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// The two races, and what losing one looks like.
///
/// Taking the last seat on a trip and being the first driver to accept a hail
/// are both read-check-write, and a transaction does not make either safe: SQL
/// Server reads at READ COMMITTED, so two callers see the same row and neither
/// blocks the other. Both rows therefore carry a row version, which makes every
/// UPDATE conditional on what was read.
///
/// What the row version itself does can only be proven against a real database,
/// and is not asserted here. What *is* asserted is everything built on top of
/// it, which is where the behaviour lives: losing is retried rather than
/// reported, a retry reads the world afresh and answers from it, sustained
/// contention gives up with <see cref="ErrorCode.Conflict"/> instead of looping,
/// and a driver who taps Accept twice ends up with one trip and is told so both
/// times. <see cref="FakeUnitOfWork.LoseNextCommits"/> stands in for the
/// exception EF raises.
/// </summary>
public class ConcurrencyTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;
    private const int OtherRiderId = 6;
    private const int TripId = 10;
    private const int PostingId = 30;

    private static BookingService Bookings(FakeUnitOfWork uow, int riderId = RiderId) =>
        Make.Bookings(uow, riderId);

    private static DriverInterestService Interests(FakeUnitOfWork uow, int driverId) =>
        Make.Interests(uow, driverId);

    /// <summary>One posted trip with <paramref name="seatsLeft"/> seats going.</summary>
    private static FakeUnitOfWork TripScene(int seatsLeft = 1)
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(DriverId));
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<User>().Add(Build.Rider(OtherRiderId));
        var trip = Build.Trip(TripId, driverId: DriverId, vehicleId: 1, seatsTotal: 4);
        trip.SeatsLeft = seatsLeft;
        uow.Store<Trip>().Add(trip);
        return uow;
    }

    // ── The last seat ────────────────────────────────────────────────────────

    [Fact]
    public async Task Losing_the_seat_race_is_retried_rather_than_reported()
    {
        // Two riders, four seats: both deserve one. Losing the write says
        // nothing about whether a seat is left, so the loser reads again — and
        // on a trip with room, the second attempt simply succeeds.
        var uow = TripScene(seatsLeft: 4);
        uow.LoseNextCommits = 1;

        var res = await Bookings(uow).Create(new CreateBookingInput { TripId = TripId, Seats = 1 });

        Assert.True(res.Success);
        Assert.Equal(1, uow.DetachCount);   // it did re-read
        Assert.Equal(BookingStatus.Confirmed, Assert.Single(uow.Store<Booking>()).Status);
    }

    [Fact]
    public async Task The_rider_who_lost_the_last_seat_is_told_there_is_none()
    {
        // The case that matters. One seat, and the winner takes it while this
        // rider is mid-write. The refusal has to come from the *fresh* read, not
        // from the fact that a write failed — those are different answers.
        var uow = TripScene(seatsLeft: 1);
        uow.LoseNextCommits = 1;
        uow.OnLostCommit = () =>
        {
            var trip = uow.Store<Trip>().Single();
            trip.SeatsLeft = 0;
            trip.Status = TripStatus.Full;
        };

        var res = await Bookings(uow).Create(new CreateBookingInput { TripId = TripId, Seats = 1 });

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.TripNotBookable, res.ErrorCode);
        Assert.Empty(uow.Store<Booking>());
    }

    [Fact]
    public async Task A_seat_is_never_sold_twice()
    {
        // The invariant the row version exists for, stated where it can be
        // stated: one seat, two riders in sequence, and the second is refused.
        var uow = TripScene(seatsLeft: 1);

        var first = await Bookings(uow, RiderId)
            .Create(new CreateBookingInput { TripId = TripId, Seats = 1 });
        var second = await Bookings(uow, OtherRiderId)
            .Create(new CreateBookingInput { TripId = TripId, Seats = 1 });

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(0, uow.Store<Trip>().Single().SeatsLeft);
        Assert.Single(uow.Store<Booking>());
    }

    [Fact]
    public async Task Sustained_contention_gives_up_instead_of_looping()
    {
        // Every attempt loses and the world never changes. Conflict rather than
        // NoSeatsLeft: there may well be a seat, we simply never got to take it,
        // and claiming otherwise would be a guess dressed as a reason.
        var uow = TripScene(seatsLeft: 4);
        uow.LoseNextCommits = ConcurrencyRules.MaxAttempts;

        var res = await Bookings(uow).Create(new CreateBookingInput { TripId = TripId, Seats = 1 });

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.Conflict, res.ErrorCode);
        Assert.Equal(ConcurrencyRules.MaxAttempts, uow.DetachCount);
    }

    [Fact]
    public async Task Giving_a_seat_back_is_retried_on_the_same_terms()
    {
        // Cancelling writes the same trip row a booking does, so it contends
        // with one — and a cancel that lost must not leave the rider still on a
        // trip they walked away from.
        var uow = TripScene(seatsLeft: 3);
        uow.Store<Booking>().Add(new Booking
        {
            Id = 1, TripId = TripId, RiderId = RiderId, Seats = 1, Status = BookingStatus.Confirmed,
        });
        uow.LoseNextCommits = 1;

        var res = await Bookings(uow).Cancel(1);

        Assert.True(res.Success);
        Assert.Equal(1, uow.DetachCount);
        Assert.Equal(BookingStatus.Cancelled, uow.Store<Booking>().Single().Status);
    }

    // ── First driver to accept wins ──────────────────────────────────────────

    /// <summary>An open posting and two verified drivers who could both take it.</summary>
    private static FakeUnitOfWork PostingScene()
    {
        var uow = new FakeUnitOfWork();
        foreach (var id in new[] { DriverId, DriverId + 1 })
        {
            var driver = Build.Driver(id);
            driver.IsOnline = true;
            driver.LastLocation = GeoFactory.Point(31.95, 35.92);
            uow.Store<User>().Add(driver);
            uow.Store<Vehicle>().Add(Build.Vehicle(id, userId: id));
        }
        uow.Store<User>().Add(Build.Rider(RiderId));
        Build.Demand(uow, PostingId, RiderId, departAt: DateTime.UtcNow.AddHours(2));
        return uow;
    }

    [Fact]
    public async Task Only_the_first_driver_to_claim_gets_a_trip()
    {
        var uow = PostingScene();

        var first = await Interests(uow, DriverId).ExpressInterest(PostingId, Offer.Shared());
        var second = await Interests(uow, DriverId + 1).ExpressInterest(PostingId, Offer.Shared());

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(ErrorCode.RideRequestNotOpen, second.ErrorCode);
        Assert.Single(uow.Store<Trip>());
        Assert.Single(uow.Store<Booking>());
    }

    [Fact]
    public async Task A_driver_who_loses_the_race_leaves_no_trip_behind()
    {
        // The trip and booking are staged before the claim is written, so losing
        // has to discard them. A stranded trip would be worse than a refusal:
        // it would sit in search offering a ride nobody is driving.
        var uow = PostingScene();
        uow.LoseNextCommits = 1;
        uow.OnLostCommit = () =>
        {
            // Another driver got there first: the request is matched to their
            // trip, and this attempt's staged rows have to go with it.
            var request = uow.Store<RideRequest>().Single();
            request.Status = RideRequestStatus.Matched;
            request.MatchedTripId = 999;
        };

        var res = await Interests(uow, DriverId).ExpressInterest(PostingId, Offer.Shared());

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.RideRequestNotOpen, res.ErrorCode);
    }

    [Fact]
    public async Task Claiming_twice_yields_one_trip_and_succeeds_both_times()
    {
        // A double tap, or a client retrying a request whose response it never
        // saw. The second call must not build a second trip, and must not tell
        // the driver holding the trip that the hail is gone.
        var uow = PostingScene();
        var service = Interests(uow, DriverId);

        var first = await service.ExpressInterest(PostingId, Offer.Shared());
        var second = await service.ExpressInterest(PostingId, Offer.Shared());

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Single(uow.Store<Trip>());
        Assert.Single(uow.Store<Booking>());
        Assert.Equal(first.Data!.Id, second.Data!.Id);
    }

    [Fact]
    public async Task A_different_driver_is_refused_rather_than_answered_idempotently()
    {
        // The idempotency check is scoped to the driver who owns the matched
        // trip. Widening it would hand the loser a success and a trip id that
        // is not theirs.
        var uow = PostingScene();
        await Interests(uow, DriverId).ExpressInterest(PostingId, Offer.Shared());

        var other = await Interests(uow, DriverId + 1).ExpressInterest(PostingId, Offer.Shared());

        Assert.False(other.Success);
        Assert.Equal(ErrorCode.RideRequestNotOpen, other.ErrorCode);
    }
}
