using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Marketplace.Models;
using Wanes.Areas.Services.RideRequests.Models;
using Wanes.Areas.Services.Safety;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// The shared, scheduled marketplace: drivers agree a trip is shared and say
/// how many seats they open, may accept on a condition, and are held to what
/// they accepted; riders compare offers on planned work, are put back on the
/// market when a driver walks away, and board with a code.
/// </summary>
public class SharedMarketplaceTests
{
    private const int RiderId = 5;
    private const int JoinerId = 6;
    private const int DriverId = 2;
    private const int OtherDriverId = 7;
    private const int AdminId = 1;

    /// <summary>Two riders, two verified drivers with four-seat cars, and an admin.</summary>
    private static FakeUnitOfWork Scene()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<User>().Add(Build.Rider(JoinerId));
        uow.Store<User>().Add(Build.Driver(DriverId));
        uow.Store<User>().Add(Build.Driver(OtherDriverId));
        uow.Store<User>().Add(new User { Id = AdminId, Phone = "+962790000000", FirstName = "Admin" });
        uow.Store<UserRole>().Add(new UserRole { Id = 1, UserId = AdminId, Role = Roles.Admin });
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));
        uow.Store<Vehicle>().Add(Build.Vehicle(2, userId: OtherDriverId));
        return uow;
    }

    private static ExpressInterestInput Shared(int? seatsOffered = null, int? minPassengers = null,
        decimal? price = 2m) => new()
    {
        AcceptSharedTrip = true,
        SeatsOffered = seatsOffered,
        MinPassengers = minPassengers,
        PricePerSeat = price,
    };

    // ── The shared-trip agreement ────────────────────────────────────────────

    [Fact]
    public async Task An_offer_that_does_not_accept_a_shared_trip_is_refused()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId);

        var res = await Make.Interests(uow, DriverId)
            .ExpressInterest(30, new ExpressInterestInput { PricePerSeat = 2m });

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.SharedTermsNotAccepted, res.ErrorCode);
        Assert.Empty(uow.Store<DriverInterest>());
    }

    [Fact]
    public async Task The_agreement_is_stamped_on_the_offer()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId);

        await Make.Interests(uow, DriverId).ExpressInterest(30, Shared());

        Assert.NotNull(uow.Store<DriverInterest>().Single().SharedTermsAcceptedAt);
    }

    // ── Seats the driver opens ───────────────────────────────────────────────

    [Fact]
    public async Task The_trip_carries_the_seats_the_driver_chose_to_open()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId, seats: 2);

        var res = await Make.Interests(uow, DriverId).ExpressInterest(30, Shared(seatsOffered: 3));

        Assert.True(res.Success);
        var trip = Assert.Single(uow.Store<Trip>());
        Assert.Equal(3, trip.SeatsTotal);
        Assert.Equal(1, trip.SeatsLeft);
    }

    [Fact]
    public async Task Opening_fewer_seats_than_the_riders_need_is_refused()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId, seats: 3);

        var res = await Make.Interests(uow, DriverId).ExpressInterest(30, Shared(seatsOffered: 2));

        Assert.Equal(ErrorCode.InvalidSeatsOffered, res.ErrorCode);
        Assert.Empty(uow.Store<Trip>());
    }

    [Fact]
    public async Task Every_booking_formed_from_demand_gets_a_boarding_code()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId);

        await Make.Interests(uow, DriverId).ExpressInterest(30, Shared());

        var booking = Assert.Single(uow.Store<Booking>());
        Assert.Matches("^[0-9]{4}$", booking.BoardingCode);
    }

    // ── The conditional accept ───────────────────────────────────────────────

    [Fact]
    public async Task A_conditional_accept_forms_a_gathering_trip()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();
        Build.Demand(uow, 30, RiderId, seats: 1);

        var res = await Make.Interests(uow, DriverId, notifications)
            .ExpressInterest(30, Shared(minPassengers: 3));

        Assert.True(res.Success);
        var trip = Assert.Single(uow.Store<Trip>());
        Assert.Equal(3, trip.MinSeatsToConfirm);
        Assert.Null(trip.ConfirmedAt);
        Assert.Equal(BookingStatus.Pending, Assert.Single(uow.Store<Booking>()).Status);
        Assert.Contains($"{RiderId}:{NotificationTemplate.RideRequestMatchedGatheringRider}", notifications.Sent);
    }

    [Fact]
    public async Task A_condition_the_pool_already_meets_confirms_at_once()
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId, seats: 3);

        await Make.Interests(uow, DriverId).ExpressInterest(30, Shared(minPassengers: 3));

        var trip = Assert.Single(uow.Store<Trip>());
        Assert.NotNull(trip.ConfirmedAt);
        Assert.Equal(BookingStatus.Confirmed, Assert.Single(uow.Store<Booking>()).Status);
    }

    // ── Instant versus scheduled selection ───────────────────────────────────

    [Fact]
    public void Instant_work_is_decided_on_the_first_offer_and_planned_work_waits()
    {
        var now = DateTime.UtcNow;

        var instant = DriverSelectionRules.DecideAt(now, now.AddMinutes(40), 0, 20);
        var planned = DriverSelectionRules.DecideAt(now, now.AddHours(5), 0, 20);

        Assert.Equal(now, instant);
        Assert.Equal(now.AddMinutes(20), planned);
    }

    [Fact]
    public void A_scheduled_decision_never_lands_inside_the_margin_before_departure()
    {
        var now = DateTime.UtcNow;

        var at = DriverSelectionRules.DecideAt(now, now.AddMinutes(70), 0, 60);

        Assert.Equal(now.AddMinutes(40), at);
    }

    [Fact]
    public async Task A_scheduled_request_collects_offers_instead_of_forming_on_the_first()
    {
        var uow = Scene();
        var config = new FakeAppConfigurationService { ScheduledSelectionWindowMinutes = 20 };
        Build.Demand(uow, 30, RiderId, departAt: DateTime.UtcNow.AddHours(5));

        var res = await Make.Interests(uow, DriverId, config: config).ExpressInterest(30, Shared());

        Assert.True(res.Success);
        Assert.Empty(uow.Store<Trip>());
        Assert.NotNull(res.Data!.DecideAt);
        Assert.True(res.Data.DecideAt > DateTime.UtcNow.AddMinutes(15));
    }

    [Fact]
    public async Task An_instant_request_still_goes_to_the_first_driver()
    {
        var uow = Scene();
        var config = new FakeAppConfigurationService { ScheduledSelectionWindowMinutes = 20 };
        Build.Demand(uow, 30, RiderId, departAt: DateTime.UtcNow.AddMinutes(40));

        await Make.Interests(uow, DriverId, config: config).ExpressInterest(30, Shared());

        Assert.Single(uow.Store<Trip>());
    }

    // ── Riders choosing ──────────────────────────────────────────────────────

    private static async Task<FakeUnitOfWork> TwoOffers(FakeAppConfigurationService config)
    {
        var uow = Scene();
        Build.Demand(uow, 30, RiderId, departAt: DateTime.UtcNow.AddHours(5));
        await Make.Interests(uow, DriverId, config: config).ExpressInterest(30, Shared(price: 3m));
        await Make.Interests(uow, OtherDriverId, config: config).ExpressInterest(30, Shared(price: 2m));
        return uow;
    }

    [Fact]
    public async Task Riders_see_the_offers_cheapest_first()
    {
        var config = new FakeAppConfigurationService { ScheduledSelectionWindowMinutes = 20 };
        var uow = await TwoOffers(config);

        var res = await Make.Interests(uow, RiderId, config: config).GetOffers(30);

        Assert.True(res.Success);
        Assert.Equal([OtherDriverId, DriverId], res.Data!.Select(o => o.DriverId));
    }

    [Fact]
    public async Task Somebody_not_on_the_request_cannot_read_its_offers()
    {
        var config = new FakeAppConfigurationService { ScheduledSelectionWindowMinutes = 20 };
        var uow = await TwoOffers(config);

        var res = await Make.Interests(uow, JoinerId, config: config).GetOffers(30);

        Assert.Equal(ErrorCode.Forbidden, res.ErrorCode);
    }

    [Fact]
    public async Task A_rider_choosing_an_offer_forms_the_trip_with_that_driver()
    {
        var config = new FakeAppConfigurationService { ScheduledSelectionWindowMinutes = 20 };
        var notifications = new FakeNotificationService();
        var uow = await TwoOffers(config);
        var chosen = uow.Store<DriverInterest>().Single(i => i.DriverId == DriverId);

        var res = await Make.Interests(uow, RiderId, notifications, config).ChooseOffer(30, chosen.Id);

        Assert.True(res.Success);
        Assert.Equal(DriverId, Assert.Single(uow.Store<Trip>()).DriverId);
        Assert.Equal(DriverInterestStatus.Rejected,
            uow.Store<DriverInterest>().Single(i => i.DriverId == OtherDriverId).Status);
        Assert.Contains($"{DriverId}:{NotificationTemplate.OfferChosenDriver}", notifications.Sent);
    }

    [Fact]
    public async Task Choosing_is_refused_when_the_setting_is_off()
    {
        var config = new FakeAppConfigurationService { ScheduledSelectionWindowMinutes = 20 };
        var uow = await TwoOffers(config);
        config.RiderOfferChoice = false;

        var res = await Make.Interests(uow, RiderId, config: config)
            .ChooseOffer(30, uow.Store<DriverInterest>()[0].Id);

        Assert.Equal(ErrorCode.OfferChoiceNotAvailable, res.ErrorCode);
        Assert.Empty(uow.Store<Trip>());
    }

    // ── Demand alerts ────────────────────────────────────────────────────────

    private static DemandAlertInput Route(int minSeats) => new()
    {
        Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
        Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
        RadiusMeters = 2000,
        MinSeats = minSeats,
    };

    [Fact]
    public async Task A_route_alert_fires_once_the_pool_reaches_the_drivers_number()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();
        await Make.Alerts(uow, DriverId).Create(Route(minSeats: 2));
        var request = Build.Demand(uow, 30, RiderId, seats: 1);

        Assert.Equal(0, await Make.Alerts(uow, DriverId, notifications).Match(request));

        await Make.Requests(uow, JoinerId, notifications).Join(30, new JoinRideRequestInput { Seats = 1 });

        Assert.Contains($"{DriverId}:{NotificationTemplate.DemandAlertMatchedDriver}", notifications.Sent);
        Assert.Single(uow.Store<DemandAlertHit>());
    }

    [Fact]
    public async Task An_alert_does_not_fire_twice_for_one_request()
    {
        var uow = Scene();
        await Make.Alerts(uow, DriverId).Create(Route(minSeats: 1));
        var request = Build.Demand(uow, 30, RiderId);

        Assert.Equal(1, await Make.Alerts(uow, DriverId).Match(request));
        Assert.Equal(0, await Make.Alerts(uow, DriverId).Match(request));
    }

    [Fact]
    public async Task An_alert_for_another_route_stays_quiet()
    {
        var uow = Scene();
        var input = Route(minSeats: 1);
        input.Destination = new GeoPoint { Lat = 32.55, Lng = 35.85, Address = "Irbid" };
        await Make.Alerts(uow, DriverId).Create(input);
        var request = Build.Demand(uow, 30, RiderId);

        Assert.Equal(0, await Make.Alerts(uow, DriverId).Match(request));
    }

    [Fact]
    public async Task A_watch_on_one_request_fires_and_retires()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();
        Build.Demand(uow, 30, RiderId, seats: 1);

        var watch = await Make.Alerts(uow, DriverId, notifications)
            .Create(new DemandAlertInput { RideRequestId = 30, MinSeats = 2 });
        Assert.True(watch.Success);
        Assert.Empty(notifications.Sent);

        await Make.Requests(uow, JoinerId, notifications).Join(30, new JoinRideRequestInput { Seats = 1 });

        Assert.Contains($"{DriverId}:{NotificationTemplate.DemandAlertMatchedDriver}", notifications.Sent);
        Assert.False(uow.Store<DemandAlert>().Single().IsActive);
    }

    // ── Cancellation and reliability ─────────────────────────────────────────

    /// <summary>A trip of the driver's with one confirmed rider on it.</summary>
    private static Trip BookedTrip(FakeUnitOfWork uow, double hoursAhead, int createdMinutesAgo = 60,
        TripStatus status = TripStatus.Posted)
    {
        var trip = Build.Trip(10, DriverId, 1, seatsTotal: 3, status: status);
        trip.DepartAt = DateTime.UtcNow.AddHours(hoursAhead);
        trip.CreationDate = DateTime.UtcNow.AddMinutes(-createdMinutesAgo);
        trip.SeatsLeft = 2;
        trip.ConfirmedAt = DateTime.UtcNow;
        uow.Store<Trip>().Add(trip);
        uow.Store<Booking>().Add(new Booking
        {
            Id = 40, TripId = 10, RiderId = RiderId, Seats = 1,
            Status = BookingStatus.Confirmed, BoardingCode = "4821",
        });
        return trip;
    }

    [Fact]
    public async Task Cancelling_a_trip_riders_depend_on_needs_a_reason()
    {
        var uow = Scene();
        BookedTrip(uow, hoursAhead: 6);

        var res = await Make.Trips(uow, DriverId).Cancel(10);

        Assert.Equal(ErrorCode.CancelReasonRequired, res.ErrorCode);
        Assert.Equal(TripStatus.Posted, uow.Store<Trip>()[0].Status);
    }

    [Fact]
    public async Task An_empty_trip_can_be_dropped_without_a_word_or_a_mark()
    {
        var uow = Scene();
        uow.Store<Trip>().Add(Build.Trip(10, DriverId, 1));

        var res = await Make.Trips(uow, DriverId).Cancel(10);

        Assert.True(res.Success);
        Assert.Equal(ReliabilityEventKind.FreeCancel, uow.Store<ReliabilityEvent>().Single().Kind);
        Assert.Equal(0, uow.Store<User>().Single(u => u.Id == DriverId).DriverCancellations);
    }

    [Fact]
    public async Task Cancelling_well_ahead_counts_one_point()
    {
        var uow = Scene();
        BookedTrip(uow, hoursAhead: 6);

        var res = await Make.Trips(uow, DriverId)
            .Cancel(10, new CancelTripInput { Reason = CancelReason.Personal });

        Assert.True(res.Success);
        var entry = uow.Store<ReliabilityEvent>().Single();
        Assert.Equal(ReliabilityEventKind.Cancel, entry.Kind);
        Assert.Equal(1, entry.Points);
        Assert.Equal(1, uow.Store<User>().Single(u => u.Id == DriverId).DriverCancellations);
    }

    [Fact]
    public async Task Cancelling_close_to_departure_counts_double()
    {
        var uow = Scene();
        BookedTrip(uow, hoursAhead: 1);

        await Make.Trips(uow, DriverId).Cancel(10, new CancelTripInput { Reason = CancelReason.Personal });

        var entry = uow.Store<ReliabilityEvent>().Single();
        Assert.Equal(ReliabilityEventKind.LateCancel, entry.Kind);
        Assert.Equal(2, entry.Points);
    }

    [Fact]
    public async Task Backing_out_right_after_accepting_is_free()
    {
        var uow = Scene();
        BookedTrip(uow, hoursAhead: 6, createdMinutesAgo: 1);

        await Make.Trips(uow, DriverId).Cancel(10, new CancelTripInput { Reason = CancelReason.Other });

        Assert.Equal(ReliabilityEventKind.FreeCancel, uow.Store<ReliabilityEvent>().Single().Kind);
    }

    [Fact]
    public async Task A_vehicle_problem_is_flagged_for_review()
    {
        var uow = Scene();
        BookedTrip(uow, hoursAhead: 6);

        await Make.Trips(uow, DriverId).Cancel(10, new CancelTripInput { Reason = CancelReason.VehicleProblem });

        Assert.True(uow.Store<ReliabilityEvent>().Single().NeedsReview);
    }

    [Fact]
    public async Task The_preview_says_what_cancelling_would_cost()
    {
        var uow = Scene();
        BookedTrip(uow, hoursAhead: 1);

        var res = await Make.Trips(uow, DriverId).CancelPreview(10);

        Assert.True(res.Success);
        Assert.Equal(ReliabilityEventKind.LateCancel, res.Data!.Kind);
        Assert.Equal(2, res.Data.Points);
        Assert.True(res.Data.ReasonRequired);
        Assert.Empty(uow.Store<ReliabilityEvent>());
    }

    [Fact]
    public async Task Enough_points_pause_instant_work_but_not_planned_work()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();
        var config = new FakeAppConfigurationService { ReliabilitySuspendPoints = 2 };
        BookedTrip(uow, hoursAhead: 1);

        await Make.Trips(uow, DriverId, notifications, config)
            .Cancel(10, new CancelTripInput { Reason = CancelReason.Personal });

        var driver = uow.Store<User>().Single(u => u.Id == DriverId);
        Assert.True(driver.SuspendedUntil > DateTime.UtcNow);
        Assert.Contains($"{DriverId}:{NotificationTemplate.ReliabilitySuspendedDriver}", notifications.Sent);

        Build.Demand(uow, 30, JoinerId, departAt: DateTime.UtcNow.AddMinutes(40));
        var instant = await Make.Interests(uow, DriverId, config: config).ExpressInterest(30, Shared());
        Assert.Equal(ErrorCode.DriverSuspended, instant.ErrorCode);

        Build.Demand(uow, 31, JoinerId, departAt: DateTime.UtcNow.AddHours(8));
        var planned = await Make.Interests(uow, DriverId, config: config).ExpressInterest(31, Shared());
        Assert.True(planned.Success);
    }

    [Fact]
    public async Task Waiving_the_entry_lifts_the_pause_and_the_count()
    {
        var uow = Scene();
        var config = new FakeAppConfigurationService { ReliabilitySuspendPoints = 2 };
        BookedTrip(uow, hoursAhead: 1);
        await Make.Trips(uow, DriverId, config: config)
            .Cancel(10, new CancelTripInput { Reason = CancelReason.VehicleProblem });
        var entry = uow.Store<ReliabilityEvent>().Single();

        var res = await Make.Reliability(uow, AdminId, config: config).Waive(entry.Id, new WaiveInput { Note = "tow receipt" });

        Assert.True(res.Success);
        var driver = uow.Store<User>().Single(u => u.Id == DriverId);
        Assert.Null(driver.SuspendedUntil);
        Assert.Equal(0, driver.DriverCancellations);
        Assert.False(entry.NeedsReview);
    }

    [Fact]
    public async Task A_rider_leaving_late_is_recorded_and_leaving_early_is_not()
    {
        var uow = Scene();
        BookedTrip(uow, hoursAhead: 1);

        await Make.Bookings(uow, RiderId).Cancel(40);

        var entry = uow.Store<ReliabilityEvent>().Single();
        Assert.Equal(ReliabilityEventKind.RiderLateCancel, entry.Kind);
        Assert.Equal(1, uow.Store<User>().Single(u => u.Id == RiderId).RiderLateCancels);

        var early = Scene();
        BookedTrip(early, hoursAhead: 8);
        await Make.Bookings(early, RiderId).Cancel(40);
        Assert.Empty(early.Store<ReliabilityEvent>());
    }

    [Fact]
    public void Completion_rate_is_unknown_for_a_new_driver()
    {
        Assert.Null(ReliabilityRules.CompletionRate(0, 0));
        Assert.Equal(0.9, ReliabilityRules.CompletionRate(9, 1));
    }

    // ── Riders put back on the market ────────────────────────────────────────

    [Fact]
    public async Task A_driver_cancelling_a_formed_trip_requeues_its_riders()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();
        Build.Demand(uow, 30, RiderId, departAt: DateTime.UtcNow.AddHours(3));
        await Make.Interests(uow, DriverId).ExpressInterest(30, Shared());
        var trip = Assert.Single(uow.Store<Trip>());

        var res = await Make.Trips(uow, DriverId, notifications)
            .Cancel(trip.Id, new CancelTripInput { Reason = CancelReason.Personal });

        Assert.True(res.Success);
        var reopened = uow.Store<RideRequest>().Single(r => r.Id != 30);
        Assert.Equal(RideRequestStatus.Open, reopened.Status);
        Assert.Equal(30, reopened.ReopenedFromRequestId);
        Assert.Contains(uow.Store<RideRequestParticipant>(),
            p => p.RideRequestId == reopened.Id && p.RiderId == RiderId);
        Assert.Contains($"{RiderId}:{NotificationTemplate.TripCancelledReopenedRider}", notifications.Sent);
        Assert.DoesNotContain($"{RiderId}:{NotificationTemplate.TripCancelledRider}", notifications.Sent);
    }

    [Fact]
    public async Task A_published_trip_cancelled_leaves_its_riders_told_plainly()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();
        BookedTrip(uow, hoursAhead: 6);

        await Make.Trips(uow, DriverId, notifications)
            .Cancel(10, new CancelTripInput { Reason = CancelReason.Personal });

        Assert.Empty(uow.Store<RideRequest>());
        Assert.Contains($"{RiderId}:{NotificationTemplate.TripCancelledRider}", notifications.Sent);
    }

    // ── Boarding codes ───────────────────────────────────────────────────────

    [Fact]
    public async Task Boarding_needs_the_riders_code()
    {
        var uow = Scene();
        var config = new FakeAppConfigurationService { BoardingCodeRequired = true };
        BookedTrip(uow, hoursAhead: 0.2);
        var trips = Make.Trips(uow, DriverId, config: config);

        var wrong = await trips.SetBookingStatus(10, 40, BookingStatus.InProgress, "0000");
        Assert.Equal(ErrorCode.BoardingCodeInvalid, wrong.ErrorCode);

        var right = await trips.SetBookingStatus(10, 40, BookingStatus.InProgress, "4821");
        Assert.True(right.Success);
        Assert.Equal(BookingStatus.InProgress, uow.Store<Booking>()[0].Status);
    }

    [Fact]
    public async Task Boarding_everyone_at_once_is_refused_while_codes_are_on()
    {
        var uow = Scene();
        var config = new FakeAppConfigurationService { BoardingCodeRequired = true };
        BookedTrip(uow, hoursAhead: 0.2);

        var res = await Make.Trips(uow, DriverId, config: config).Start(10);

        Assert.Equal(ErrorCode.BoardingCodeRequired, res.ErrorCode);
        Assert.Equal(BookingStatus.Confirmed, uow.Store<Booking>()[0].Status);
    }

    [Fact]
    public async Task The_rider_sees_their_code_only_while_it_is_useful()
    {
        var uow = Scene();
        BookedTrip(uow, hoursAhead: 1);

        var mine = await Make.Bookings(uow, RiderId).GetUserBookings();
        Assert.Equal("4821", Assert.Single(mine.Data!).BoardingCode);

        uow.Store<Booking>()[0].Status = BookingStatus.InProgress;
        mine = await Make.Bookings(uow, RiderId).GetUserBookings();
        Assert.Null(Assert.Single(mine.Data!).BoardingCode);
    }

    [Fact]
    public async Task A_no_show_goes_on_the_riders_record()
    {
        var uow = Scene();
        BookedTrip(uow, hoursAhead: 0.2);

        await Make.Trips(uow, DriverId).SetBookingStatus(10, 40, BookingStatus.NoShow);

        Assert.Equal(ReliabilityEventKind.RiderNoShow, uow.Store<ReliabilityEvent>().Single().Kind);
        Assert.Equal(1, uow.Store<User>().Single(u => u.Id == RiderId).RiderNoShows);
    }

    // ── Safety ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_sos_reaches_the_admin_team()
    {
        var uow = Scene();
        var notifications = new FakeNotificationService();
        BookedTrip(uow, hoursAhead: 0.2);

        var res = await Make.Safety(uow, RiderId, notifications).Raise(new RaiseSafetyInput
        {
            Kind = SafetyIncidentKind.Sos, TripId = 10, Lat = 31.95, Lng = 35.92,
        });

        Assert.True(res.Success);
        Assert.Equal("911", res.Data!.EmergencyNumber);
        var incident = uow.Store<SafetyIncident>().Single();
        Assert.Equal(40, incident.BookingId);
        Assert.Contains($"{AdminId}:{NotificationTemplate.SafetyIncidentAdmin}", notifications.Sent);
    }

    [Fact]
    public async Task Nobody_can_attach_a_report_to_a_trip_they_are_not_on()
    {
        var uow = Scene();
        BookedTrip(uow, hoursAhead: 0.2);

        var res = await Make.Safety(uow, JoinerId).Raise(new RaiseSafetyInput { TripId = 10 });

        Assert.Equal(ErrorCode.Forbidden, res.ErrorCode);
        Assert.Empty(uow.Store<SafetyIncident>());
    }

    [Fact]
    public async Task An_sos_messages_the_emergency_contact_with_a_trip_link()
    {
        var uow = Scene();
        uow.Store<User>().Single(u => u.Id == RiderId).EmergencyContactPhone = "+962790001111";
        BookedTrip(uow, hoursAhead: 0.2);

        await Make.Safety(uow, RiderId).Raise(new RaiseSafetyInput { Kind = SafetyIncidentKind.Sos, TripId = 10 });

        Assert.True(uow.Store<SafetyIncident>().Single().EmergencyContactNotified);
        Assert.NotNull(uow.Store<Booking>()[0].ShareToken);
    }

    [Fact]
    public async Task A_shared_link_shows_the_ride_and_hides_the_car_until_it_moves()
    {
        var uow = Scene();
        var config = new FakeAppConfigurationService { ShareBaseUrl = "https://wanes.app/" };
        var trip = BookedTrip(uow, hoursAhead: 1);
        trip.Vehicle = uow.Store<Vehicle>()[0];
        trip.Driver = uow.Store<User>().Single(u => u.Id == DriverId);
        trip.Driver.LastLocation = GeoFactory.Point(31.96, 35.91);
        uow.Store<Booking>()[0].Trip = trip;
        uow.Store<Booking>()[0].Rider = uow.Store<User>().Single(u => u.Id == RiderId);

        var link = await Make.Safety(uow, RiderId, config: config).CreateShareLink(40);
        Assert.True(link.Success);
        Assert.Equal($"https://wanes.app/en/share/{link.Data!.Token}", link.Data.Url);

        var shared = await Make.Safety(uow, 0).GetShared(link.Data.Token);
        Assert.True(shared.Success);
        Assert.Equal("Rdr", shared.Data!.RiderFirstName);
        Assert.Null(shared.Data.DriverLat);

        trip.Status = TripStatus.EnRoute;
        shared = await Make.Safety(uow, 0).GetShared(link.Data.Token);
        Assert.Equal(31.96, shared.Data!.DriverLat!.Value, 3);

        await Make.Safety(uow, RiderId).RevokeShareLink(40);
        var gone = await Make.Safety(uow, 0).GetShared(link.Data.Token);
        Assert.Equal(ErrorCode.ShareLinkNotFound, gone.ErrorCode);
    }

    // ── The rules on their own ───────────────────────────────────────────────

    [Theory]
    [InlineData(0, 300, 60, TripStatus.Posted, ReliabilityEventKind.FreeCancel)]      // nobody aboard
    [InlineData(2, 300, 1, TripStatus.Posted, ReliabilityEventKind.FreeCancel)]       // mis-tap
    [InlineData(2, 300, 60, TripStatus.Posted, ReliabilityEventKind.Cancel)]
    [InlineData(2, 90, 60, TripStatus.Posted, ReliabilityEventKind.LateCancel)]       // inside two hours
    [InlineData(2, 90, 1, TripStatus.Posted, ReliabilityEventKind.LateCancel)]        // grace does not cover late
    [InlineData(2, 300, 60, TripStatus.EnRoute, ReliabilityEventKind.LateCancel)]     // already on the road
    public void Cancellations_are_classified_by_who_depends_on_the_trip_and_when(
        int riders, int minutesToDeparture, int minutesSinceAccept, TripStatus status, ReliabilityEventKind expected)
    {
        var now = DateTime.UtcNow;
        var kind = ReliabilityRules.ClassifyDriverCancel(now,
            acceptedAt: now.AddMinutes(-minutesSinceAccept),
            departAt: now.AddMinutes(minutesToDeparture),
            status, riders,
            ReliabilityRules.DefaultFreeCancelGraceMinutes,
            ReliabilityRules.DefaultLateCancelLeadMinutes);

        Assert.Equal(expected, kind);
    }
}
