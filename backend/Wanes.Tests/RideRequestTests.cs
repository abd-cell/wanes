using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.RideRequests.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// Demand, end to end: a rider creates a request, other riders join it, a driver
/// offers to serve it, and one offer becomes a trip.
///
/// Five rules carry the whole flow and each is pinned here — the departure has
/// to leave a driver room to gather everybody, a pool's conditions intersect
/// rather than being overwritten, the request dies with its last participant,
/// an offer is a signal and not a trip, and formation turns every participant
/// into exactly one booking on one trip.
///
/// Where the old suite asserted a claim mutated a trip in place, these assert
/// the request is **matched to** a trip. That is the v2 change, and the one
/// thing it costs is asserted too: the id changes, and
/// <see cref="RideRequestRow.MatchedTripId"/> is the thread across it.
/// </summary>
public class RideRequestTests
{
    private const int RiderId = 5;
    private const int JoinerId = 6;
    private const int DriverId = 2;

    /// <summary>Two riders and a verified driver with a four-seat car.</summary>
    private static FakeUnitOfWork Scene()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<User>().Add(Build.Rider(JoinerId));

        var driver = Build.Driver(DriverId);
        driver.IsOnline = true;
        driver.LastLocation = GeoFactory.Point(31.95, 35.92);
        uow.Store<User>().Add(driver);
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));
        return uow;
    }

    private static CreateRideRequestInput Asking(int seats = 1, double hoursAhead = 4) => new()
    {
        Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
        Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
        DepartAt = DateTime.UtcNow.AddHours(hoursAhead),
        Seats = seats,
    };

    // ── Creating demand ──────────────────────────────────────────────────────

    [Fact]
    public async Task Creating_demand_puts_the_author_on_it()
    {
        var uow = Scene();

        var res = await Make.Requests(uow, RiderId).Create(Asking(seats: 2));

        Assert.True(res.Success);
        Assert.Equal(2, res.Data!.SeatsWanted);
        Assert.Equal(2, res.Data.MySeats);
        Assert.Equal(1, res.Data.RiderCount);
        Assert.Null(res.Data.MatchedTripId);

        var participant = Assert.Single(uow.Store<RideRequestParticipant>());
        Assert.Equal(RiderId, participant.RiderId);
        Assert.Equal(2, participant.Seats);

        // And no trip. Demand is not transportation that exists.
        Assert.Empty(uow.Store<Trip>());
        Assert.Empty(uow.Store<Booking>());
    }

    [Fact]
    public async Task A_departure_too_soon_for_the_seats_asked_for_is_refused()
    {
        var uow = Scene();

        // Four seats over this leg needs hours of gathering, not twenty minutes.
        var res = await Make.Requests(uow, RiderId)
            .Create(Asking(seats: 4, hoursAhead: 0.3));

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.DepartureTooSoon, res.ErrorCode);
    }

    [Fact]
    public async Task One_seat_needs_only_the_floor_of_lead_time()
    {
        var uow = Scene();

        var res = await Make.Requests(uow, RiderId).Create(Asking(seats: 1, hoursAhead: 0.5));

        Assert.True(res.Success);
    }

    [Fact]
    public async Task A_request_wanted_within_the_hour_reaches_drivers_at_once()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();

        var res = await Make.Requests(uow, RiderId, notifications)
            .Create(Asking(seats: 1, hoursAhead: 0.5));

        Assert.True(res.Success);
        Assert.Contains($"nearby:{res.Data!.Id}", notifications.Sent);
        Assert.NotNull(uow.Store<RideRequest>()[0].NotifiedAt);
    }

    [Fact]
    public async Task A_request_for_next_week_waits_on_the_board_instead()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();

        var res = await Make.Requests(uow, RiderId, notifications)
            .Create(Asking(seats: 1, hoursAhead: 24 * 7));

        Assert.True(res.Success);

        // On the board from the moment it is written — this is only about
        // whether anybody's phone is interrupted on a Monday for a Thursday ride.
        Assert.DoesNotContain($"nearby:{res.Data!.Id}", notifications.Sent);
        Assert.Null(uow.Store<RideRequest>()[0].NotifiedAt);
    }

    [Fact]
    public async Task The_notify_sweep_pushes_a_request_once_it_comes_into_range()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();
        Build.Demand(uow, 30, RiderId, departAt: DateTime.UtcNow.AddMinutes(40));

        var pushed = await Make.Requests(uow, DriverId, notifications).NotifyDue();

        Assert.Equal(1, pushed);
        Assert.Contains("nearby:30", notifications.Sent);

        // Once. The stamp is what makes that true across sweeps.
        Assert.Equal(0, await Make.Requests(uow, DriverId, notifications).NotifyDue());
    }

    // ── Joining ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_second_rider_joins_and_the_seats_add_up()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId, seats: 1);

        var res = await Make.Requests(uow, JoinerId).Join(30, new JoinRideRequestInput { Seats = 2 });

        Assert.True(res.Success);
        Assert.Equal(3, res.Data!.SeatsWanted);
        Assert.Equal(2, res.Data.RiderCount);
        Assert.Equal(3, uow.Store<RideRequest>()[0].SeatsRequested);
    }

    [Fact]
    public async Task A_rider_cannot_join_the_same_request_twice()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId);

        var res = await Make.Requests(uow, RiderId).Join(30, new JoinRideRequestInput());

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.AlreadyJoined, res.ErrorCode);
    }

    [Fact]
    public async Task Joining_past_what_one_car_can_carry_is_refused()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId, seats: RiderTripRules.MaxSeats);

        var res = await Make.Requests(uow, JoinerId).Join(30, new JoinRideRequestInput { Seats = 1 });

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.RideRequestSeatsExceeded, res.ErrorCode);
    }

    [Fact]
    public async Task A_joiner_the_pool_would_not_accept_is_refused()
    {
        var uow = Scene();
        var request = Build.Demand(uow, 30, RiderId);
        request.GenderPolicy = GenderPolicy.FemaleOnly;

        var joiner = uow.Store<User>().Single(u => u.Id == JoinerId);
        joiner.Gender = Gender.Male;

        var res = await Make.Requests(uow, JoinerId).Join(30, new JoinRideRequestInput());

        Assert.False(res.Success);
    }

    [Fact]
    public async Task A_joiner_who_would_exclude_somebody_already_on_it_is_refused()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId);
        uow.Store<User>().Single(u => u.Id == RiderId).Gender = Gender.Male;
        uow.Store<User>().Single(u => u.Id == JoinerId).Gender = Gender.Female;

        // The newcomer will only share with women — and there is a man on it.
        // They are the one who has to look elsewhere: he committed first, and a
        // pool whose membership changes under its members is the outcome to
        // rule out.
        var res = await Make.Requests(uow, JoinerId)
            .Join(30, new JoinRideRequestInput { CoRiderGenderPolicy = GenderPolicy.FemaleOnly });

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.RideRequestConditionsConflict, res.ErrorCode);
    }

    [Fact]
    public async Task A_joiners_conditions_tighten_the_pool_rather_than_replacing_them()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId);
        uow.Store<User>().Single(u => u.Id == RiderId).Gender = Gender.Female;
        uow.Store<User>().Single(u => u.Id == JoinerId).Gender = Gender.Female;

        var res = await Make.Requests(uow, JoinerId)
            .Join(30, new JoinRideRequestInput { CoRiderGenderPolicy = GenderPolicy.FemaleOnly });

        Assert.True(res.Success);
        Assert.Equal(GenderPolicy.FemaleOnly, uow.Store<RideRequest>()[0].GenderPolicy);
    }

    [Fact]
    public async Task Two_opposed_driver_conditions_leave_nobody_to_drive()
    {
        var uow = Scene();
        var request = Build.Demand(uow, 30, RiderId);
        request.DriverGenderPolicy = GenderPolicy.FemaleOnly;

        var res = await Make.Requests(uow, JoinerId)
            .Join(30, new JoinRideRequestInput { DriverGenderPolicy = GenderPolicy.MaleOnly });

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.RideRequestConditionsConflict, res.ErrorCode);
    }

    // ── Leaving ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Leaving_releases_the_seats_and_leaves_the_pool_standing()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId, seats: 1);
        await Make.Requests(uow, JoinerId).Join(30, new JoinRideRequestInput { Seats = 2 });

        var res = await Make.Requests(uow, JoinerId).Leave(30);

        Assert.True(res.Success);
        Assert.Equal(RideRequestStatus.Open, uow.Store<RideRequest>()[0].Status);
        Assert.Equal(1, uow.Store<RideRequest>()[0].SeatsRequested);
    }

    [Fact]
    public async Task The_last_rider_leaving_closes_the_request_on_every_screen()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();
        Build.Demand(uow, 30, RiderId);

        var res = await Make.Requests(uow, RiderId, notifications).Leave(30);

        Assert.True(res.Success);
        Assert.Equal(RideRequestStatus.Cancelled, uow.Store<RideRequest>()[0].Status);

        // Filtering it out of the next board is not enough — a driver staring at
        // the card would still tap on a ride nobody wants any more.
        Assert.Contains($"request-30:{RiderTripClosedReason.Cancelled}", notifications.Sent);
    }

    [Fact]
    public async Task The_author_leaving_does_not_dissolve_a_pool_others_are_on()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId);
        await Make.Requests(uow, JoinerId).Join(30, new JoinRideRequestInput());

        await Make.Requests(uow, RiderId).Leave(30);

        // Being first confers nothing. There is no author column at all.
        Assert.Equal(RideRequestStatus.Open, uow.Store<RideRequest>()[0].Status);
    }

    [Fact]
    public async Task A_rider_who_is_not_on_it_has_nothing_to_leave()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId);

        var res = await Make.Requests(uow, JoinerId).Leave(30);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.RideRequestNotJoined, res.ErrorCode);
    }

    // ── Interest, selection and formation ────────────────────────────────────

    [Fact]
    public async Task An_offer_forms_a_trip_with_the_spare_seats_on_sale()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId, seats: 1, departAt: DateTime.UtcNow.AddHours(4));

        var res = await Make.Interests(uow, DriverId)
            .ExpressInterest(30, new ExpressInterestInput { AcceptSharedTrip = true, PricePerSeat = 4m });

        Assert.True(res.Success);

        // The request is matched, and points at the ride it became.
        var request = uow.Store<RideRequest>()[0];
        Assert.Equal(RideRequestStatus.Matched, request.Status);
        Assert.NotNull(request.MatchedTripId);
        Assert.Equal(request.MatchedTripId, res.Data!.MatchedTripId);

        // The trip is an ordinary trip: the driver's car, their price, and the
        // seats the pool did not want are on sale to anybody.
        var trip = Assert.Single(uow.Store<Trip>());
        Assert.Equal(DriverId, trip.DriverId);
        Assert.Equal(4m, trip.PricePerSeat);
        Assert.Equal(4, trip.SeatsTotal);
        Assert.Equal(3, trip.SeatsLeft);
        Assert.Equal(TripStatus.Posted, trip.Status);
    }

    [Fact]
    public async Task Every_participant_becomes_exactly_one_confirmed_booking()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId, seats: 1);
        await Make.Requests(uow, JoinerId).Join(30, new JoinRideRequestInput { Seats = 2 });

        await Make.Interests(uow, DriverId)
            .ExpressInterest(30, new ExpressInterestInput { AcceptSharedTrip = true, PricePerSeat = 3m });

        // Two participants, two bookings. A conversion that lost a rider would
        // put somebody at a kerb the driver was never told about; one that
        // duplicated a rider would sell the same seat twice.
        var bookings = uow.Store<Booking>();
        Assert.Equal(2, bookings.Count);
        Assert.All(bookings, b => Assert.Equal(BookingStatus.Confirmed, b.Status));
        Assert.Equal(3, bookings.Sum(b => b.Seats));
        Assert.Equal([RiderId, JoinerId], bookings.Select(b => b.RiderId).Order().ToList());
    }

    [Fact]
    public async Task A_trip_formed_from_demand_is_confirmed_not_gathering()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId);

        await Make.Interests(uow, DriverId).ExpressInterest(30, Offer.Shared());

        // Its passengers are already secured — they asked for this ride. There is
        // nothing to gather and no threshold to fail.
        var trip = Assert.Single(uow.Store<Trip>());
        Assert.True(trip.IsConfirmed);
        Assert.Equal(TripConfirmationRules.NoThreshold, trip.MinSeatsToConfirm);
    }

    [Fact]
    public async Task The_formed_trip_departs_at_the_hour_the_riders_wanted()
    {
        var uow = Scene();
        var wanted = DateTime.UtcNow.AddHours(5);
        Build.Demand(uow, 30, RiderId, departAt: wanted);

        await Make.Interests(uow, DriverId).ExpressInterest(30, Offer.Shared());

        var trip = Assert.Single(uow.Store<Trip>());
        Assert.Equal(wanted, trip.DepartAt, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task The_riders_conditions_govern_the_spare_seats_too()
    {
        var uow = Scene();
        var request = Build.Demand(uow, 30, RiderId);
        request.GenderPolicy = GenderPolicy.FemaleOnly;
        request.MinAge = 21;

        await Make.Interests(uow, DriverId).ExpressInterest(30, Offer.Shared());

        // A pool that asked to share with women only keeps that condition over
        // the seats the driver is now selling — which is what they agreed to.
        var trip = Assert.Single(uow.Store<Trip>());
        Assert.Equal(GenderPolicy.FemaleOnly, trip.GenderPolicy);
        Assert.Equal(21, trip.MinAge);
    }

    [Fact]
    public async Task The_riders_are_told_a_driver_took_it_and_given_the_trip()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();
        Build.Demand(uow, 30, RiderId);

        await Make.Interests(uow, DriverId, notifications).ExpressInterest(30, Offer.Shared());

        Assert.Contains($"{RiderId}:{NotificationTemplate.RideRequestMatchedRider}",
            notifications.Sent);

        // And every driver still holding the card loses it.
        Assert.Contains($"request-30:{RiderTripClosedReason.Claimed}", notifications.Sent);
    }

    [Fact]
    public async Task A_driver_the_riders_ruled_out_cannot_offer()
    {
        var uow = Scene();
        var request = Build.Demand(uow, 30, RiderId);
        request.DriverGenderPolicy = GenderPolicy.FemaleOnly;
        uow.Store<User>().Single(u => u.Id == DriverId).Gender = Gender.Male;

        var res = await Make.Interests(uow, DriverId).ExpressInterest(30, Offer.Shared());

        Assert.False(res.Success);
        Assert.Empty(uow.Store<Trip>());
    }

    [Fact]
    public async Task Nobody_serves_their_own_request()
    {
        var uow = Scene();
        Build.Demand(uow, 30, DriverId);

        var res = await Make.Interests(uow, DriverId).ExpressInterest(30, Offer.Shared());

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.CannotServeOwnRequest, res.ErrorCode);
    }

    [Fact]
    public async Task An_unverified_driver_cannot_offer()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId);
        uow.Store<User>().Single(u => u.Id == DriverId).DriverStatus = DriverStatus.Pending;

        var res = await Make.Interests(uow, DriverId).ExpressInterest(30, Offer.Shared());

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.DriverNotVerified, res.ErrorCode);
    }

    [Fact]
    public async Task A_pool_bigger_than_the_car_is_refused()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId, seats: 6);

        var res = await Make.Interests(uow, DriverId).ExpressInterest(30, Offer.Shared());

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.SeatsExceedCapacity, res.ErrorCode);
    }

    [Fact]
    public async Task A_driver_may_withdraw_while_offers_are_still_open()
    {
        var uow = Scene();
        var config = new FakeAppConfigurationService { ScheduledSelectionWindowMinutes = 10 };
        Build.Demand(uow, 30, RiderId);

        await Make.Interests(uow, DriverId, config: config).ExpressInterest(30, Offer.Shared());
        var res = await Make.Interests(uow, DriverId, config: config).WithdrawInterest(30);

        Assert.True(res.Success);
        Assert.Equal(DriverInterestStatus.Withdrawn, uow.Store<DriverInterest>()[0].Status);
        Assert.Empty(uow.Store<Trip>());
    }

    // ── The selection window ─────────────────────────────────────────────────

    [Fact]
    public async Task With_a_window_open_an_offer_is_recorded_and_no_trip_is_formed()
    {
        var uow = Scene();
        var config = new FakeAppConfigurationService { ScheduledSelectionWindowMinutes = 10 };
        var notifications = new FakeNotificationService();
        Build.Demand(uow, 30, RiderId);

        var res = await Make.Interests(uow, DriverId, notifications, config).ExpressInterest(30, Offer.Shared());

        Assert.True(res.Success);
        Assert.Equal(1, res.Data!.InterestCount);
        Assert.True(res.Data.IHaveOffered);

        // Interest is a signal, not a trip.
        Assert.Empty(uow.Store<Trip>());
        Assert.Equal(RideRequestStatus.Open, uow.Store<RideRequest>()[0].Status);

        // The riders hear that somebody is willing — the one thing that visibly
        // moves while they wait.
        Assert.Contains($"{RiderId}:{NotificationTemplate.RideRequestInterestRider}",
            notifications.Sent);
    }

    [Fact]
    public async Task The_window_closing_picks_a_driver_and_tells_the_rest()
    {
        var uow = Scene();
        var config = new FakeAppConfigurationService { ScheduledSelectionWindowMinutes = 10 };
        var notifications = new FakeNotificationService();

        // A second driver, better rated, offering later.
        const int OtherDriverId = 7;
        var other = Build.Driver(OtherDriverId);
        other.RatingAvg = 4.9;
        uow.Store<User>().Add(other);
        uow.Store<Vehicle>().Add(Build.Vehicle(2, userId: OtherDriverId));

        Build.Demand(uow, 30, RiderId);
        await Make.Interests(uow, DriverId, config: config).ExpressInterest(30, Offer.Shared());
        await Make.Interests(uow, OtherDriverId, config: config).ExpressInterest(30, Offer.Shared());

        // The window opened when the first offer arrived; wind it back so it is due.
        uow.Store<RideRequest>()[0].FirstInterestAt = DateTime.UtcNow.AddMinutes(-30);
        uow.Store<RideRequest>()[0].DecideAt = DateTime.UtcNow.AddMinutes(-20);

        var matched = await Make.Interests(uow, DriverId, notifications, config).SelectDue();

        Assert.Equal(1, matched);

        // Rating outranks being first — which is the whole difference between a
        // window and first-come-first-served.
        var trip = Assert.Single(uow.Store<Trip>());
        Assert.Equal(OtherDriverId, trip.DriverId);

        var interests = uow.Store<DriverInterest>();
        Assert.Equal(DriverInterestStatus.Selected,
            interests.Single(i => i.DriverId == OtherDriverId).Status);
        Assert.Equal(DriverInterestStatus.Rejected,
            interests.Single(i => i.DriverId == DriverId).Status);

        // The loser is told. Silence would leave them unable to tell "somebody
        // else got it" from "the app is broken".
        Assert.Contains($"{DriverId}:{NotificationTemplate.RideRequestNotSelectedDriver}",
            notifications.Sent);
    }

    [Fact]
    public async Task With_no_window_the_sweep_has_nothing_to_do()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId);
        await Make.Interests(uow, DriverId).ExpressInterest(30, Offer.Shared());

        // Immediate selection decided it inside the offer. A sweep that also
        // tried would be a second decision about a settled request.
        Assert.Equal(0, await Make.Interests(uow, DriverId).SelectDue());
    }

    // ── Expiry ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_request_whose_departure_has_come_expires_and_leaves_the_screens()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();
        Build.Demand(uow, 30, RiderId, departAt: DateTime.UtcNow.AddMinutes(-1));

        var swept = await Make.Requests(uow, RiderId, notifications).ExpireDue();

        Assert.Equal(1, swept);
        Assert.Equal(RideRequestStatus.Expired, uow.Store<RideRequest>()[0].Status);
        Assert.Contains($"request-30:{RiderTripClosedReason.Expired}", notifications.Sent);
    }

    [Fact]
    public async Task A_request_past_its_departure_cannot_still_be_served()
    {
        var uow = Scene();

        // Between the departure passing and the next sweep the row still reads
        // Open. The clock is the authority, not the column.
        Build.Demand(uow, 30, RiderId, departAt: DateTime.UtcNow.AddMinutes(-5));

        var res = await Make.Interests(uow, DriverId).ExpressInterest(30, Offer.Shared());

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.RideRequestNotOpen, res.ErrorCode);
    }

    [Fact]
    public async Task Requests_the_caller_joined_come_back_as_theirs()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId);
        await Make.Requests(uow, JoinerId).Join(30, new JoinRideRequestInput());

        var res = await Make.Requests(uow, JoinerId).GetMine();

        Assert.True(res.Success);
        var row = Assert.Single(res.Data!);
        Assert.Equal(30, row.Id);
        Assert.Equal(1, row.MySeats);
    }
}
