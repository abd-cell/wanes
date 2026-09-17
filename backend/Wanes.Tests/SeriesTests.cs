using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Schedules;
using Wanes.Areas.Domain.Series;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Marketplace.Models;
using Wanes.Areas.Services.RideRequests.Models;
using Wanes.Areas.Services.Schedules.Models;
using Wanes.Areas.Services.Series.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// Committing to a whole series: a driver taking a rider's commute, a rider
/// booking every day of a driver's. What stays true throughout is §11.2 —
/// every day is still its own trip with its own seats — so most assertions
/// are about the ordinary rows a commitment produced.
/// </summary>
public class SeriesTests
{
    private const int RiderId = 5;
    private const int OtherRiderId = 6;
    private const int DriverId = 2;
    private const int OtherDriverId = 7;

    private static FakeUnitOfWork Scene()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<User>().Add(Build.Rider(OtherRiderId));
        uow.Store<User>().Add(Build.Driver(DriverId));
        uow.Store<User>().Add(Build.Driver(OtherDriverId));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));
        uow.Store<Vehicle>().Add(Build.Vehicle(2, userId: OtherDriverId));
        return uow;
    }

    /// <summary>Every day at 07:30 UTC from tomorrow, for either side.</summary>
    private static TripScheduleInput Daily(ActiveRole role) => new()
    {
        OwnerRole = role,
        Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
        Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
        Recurrence = Recurrence.Daily,
        TimeOfDay = new TimeOnly(7, 30),
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1),
        Seats = role == ActiveRole.Driver ? 3 : 1,
        PricePerSeat = role == ActiveRole.Driver ? 3m : null,
        VehicleId = role == ActiveRole.Driver ? 1 : null,
    };

    private static ProposeSeriesInput Offer(decimal price = 2m, WeekDays days = WeekDays.None) => new()
    {
        AcceptSharedTrip = true,
        PricePerSeat = price,
        DaysOfWeek = days,
    };

    /// <summary>A rider's daily commute, materialised: open requests for a fortnight.</summary>
    private static async Task<(FakeUnitOfWork Uow, int ScheduleId, RideRequest First)> RiderCommute(
        FakeAppConfigurationService? config = null)
    {
        var uow = Scene();
        var created = await Make.Schedules(uow, RiderId, config).Create(Daily(ActiveRole.Rider));
        await Make.Schedules(uow, RiderId, config).MaterialiseDue();
        var first = uow.Store<RideRequest>().OrderBy(r => r.DepartAt).First();
        return (uow, created.Data!.Id, first);
    }

    /// <summary>A driver's daily trip, materialised: posted trips for a fortnight.</summary>
    private static async Task<(FakeUnitOfWork Uow, int ScheduleId, Trip First)> DriverCommute(
        FakeAppConfigurationService? config = null)
    {
        var uow = Scene();
        var created = await Make.Schedules(uow, DriverId, config).Create(Daily(ActiveRole.Driver));
        await Make.Schedules(uow, DriverId, config).MaterialiseDue();
        var first = uow.Store<Trip>().OrderBy(t => t.DepartAt).First();
        return (uow, created.Data!.Id, first);
    }

    // ── Rules ────────────────────────────────────────────────────────────────

    [Fact]
    public void Skipping_a_day_with_notice_is_free_until_the_free_skips_run_out()
    {
        var now = DateTime.UtcNow;
        var inTwoDays = now.AddDays(2);

        Assert.Equal(ReliabilityEventKind.SeriesSkip,
            SeriesRules.ClassifySkip(now, inTwoDays, TripStatus.Posted, 2, 24, freeSkipsUsed: 0, freeSkipsAllowed: 4));
        Assert.Equal(ReliabilityEventKind.Cancel,
            SeriesRules.ClassifySkip(now, inTwoDays, TripStatus.Posted, 2, 24, freeSkipsUsed: 4, freeSkipsAllowed: 4));
        Assert.Equal(ReliabilityEventKind.LateCancel,
            SeriesRules.ClassifySkip(now, now.AddHours(10), TripStatus.Posted, 2, 24, 0, 4));
        Assert.Equal(ReliabilityEventKind.FreeCancel,
            SeriesRules.ClassifySkip(now, now.AddHours(10), TripStatus.Posted, 0, 24, 0, 4));
    }

    [Fact]
    public void A_day_subset_must_overlap_the_schedule()
    {
        var sunToThu = new TripSchedule
        {
            Recurrence = Recurrence.Weekly,
            DaysOfWeek = WeekDays.Sunday | WeekDays.Monday | WeekDays.Tuesday | WeekDays.Wednesday | WeekDays.Thursday,
        };

        Assert.Equal(WeekDays.Monday, SeriesRules.NormaliseDays(WeekDays.Monday | WeekDays.Friday, sunToThu));
        Assert.Null(SeriesRules.NormaliseDays(WeekDays.Friday, sunToThu));
        // Naming every day the schedule runs is the same as naming none.
        Assert.Equal(WeekDays.None, SeriesRules.NormaliseDays(sunToThu.DaysOfWeek, sunToThu));
    }

    [Fact]
    public void Consecutive_days_read_as_a_range()
    {
        var sunToThu = new TripSchedule
        {
            Recurrence = Recurrence.Weekly,
            DaysOfWeek = WeekDays.Sunday | WeekDays.Monday | WeekDays.Tuesday | WeekDays.Wednesday | WeekDays.Thursday,
        };

        Assert.Equal("Sun–Thu", SeriesRules.DaysLabel(sunToThu, WeekDays.None).En);
        Assert.Equal("Mon, Wed", SeriesRules.DaysLabel(sunToThu, WeekDays.Monday | WeekDays.Wednesday).En);
        Assert.Equal("Every day", SeriesRules.DaysLabel(new TripSchedule { Recurrence = Recurrence.Daily }, WeekDays.None).En);
    }

    // ── A driver takes a rider's series ──────────────────────────────────────

    [Fact]
    public async Task A_one_off_request_cannot_be_taken_as_a_series()
    {
        var uow = Scene();
        Build.Demand(uow, 40, RiderId);

        var res = await Make.Series(uow, DriverId).Propose(40, Offer());

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.NotRecurring, res.ErrorCode);
    }

    [Fact]
    public async Task A_series_offer_must_accept_the_shared_trip()
    {
        var (uow, _, first) = await RiderCommute();

        var res = await Make.Series(uow, DriverId).Propose(first.Id, new ProposeSeriesInput { PricePerSeat = 2m });

        Assert.Equal(ErrorCode.SharedTermsNotAccepted, res.ErrorCode);
    }

    [Fact]
    public async Task An_offer_waits_for_the_rider_and_tells_them()
    {
        var (uow, scheduleId, first) = await RiderCommute();
        var notifications = new FakeNotificationService();

        var res = await Make.Series(uow, DriverId, notifications).Propose(first.Id, Offer());

        Assert.True(res.Success);
        Assert.Equal(SeriesStatus.Proposed, res.Data!.Status);
        Assert.Equal(scheduleId, res.Data.ScheduleId);
        Assert.NotNull(res.Data.DecideAt);
        Assert.Contains($"{RiderId}:{NotificationTemplate.SeriesOfferRider}", notifications.Sent);
        // Nothing is formed until the rider answers.
        Assert.All(uow.Store<RideRequest>(), r => Assert.Equal(RideRequestStatus.Open, r.Status));
    }

    [Fact]
    public async Task Accepting_gives_the_driver_every_upcoming_day_and_answers_the_others()
    {
        var (uow, scheduleId, first) = await RiderCommute();
        var mine = await Make.Series(uow, DriverId).Propose(first.Id, Offer(2m));
        var theirs = await Make.Series(uow, OtherDriverId).Propose(first.Id, Offer(1.5m));
        var notifications = new FakeNotificationService();

        var offers = await Make.Series(uow, RiderId).OffersFor(scheduleId);
        Assert.Equal(2, offers.Data!.Count);

        var res = await Make.Series(uow, RiderId, notifications).Accept(mine.Data!.Id);

        Assert.True(res.Success);
        Assert.Equal(SeriesStatus.Active, res.Data!.Series.Status);
        var requests = uow.Store<RideRequest>();
        Assert.All(requests, r => Assert.Equal(RideRequestStatus.Matched, r.Status));
        Assert.Equal(requests.Count, res.Data.Days.Count(d => d.TripId != null));

        // Every day is an ordinary trip, driven by the series driver, with the rider's seat.
        var trips = uow.Store<Trip>();
        Assert.Equal(requests.Count, trips.Count);
        Assert.All(trips, t =>
        {
            Assert.Equal(DriverId, t.DriverId);
            Assert.Equal(mine.Data.Id, t.SeriesCommitmentId);
            Assert.Equal(2m, t.PricePerSeat);
            Assert.Single(uow.Store<Booking>(), b => b.TripId == t.Id && b.RiderId == RiderId);
        });

        Assert.Equal(SeriesStatus.Declined,
            uow.Store<SeriesCommitment>().Single(c => c.Id == theirs.Data!.Id).Status);
        Assert.Contains($"{OtherDriverId}:{NotificationTemplate.SeriesNotSelectedDriver}", notifications.Sent);
        Assert.Contains($"{DriverId}:{NotificationTemplate.SeriesAcceptedDriver}", notifications.Sent);
        // One message for the series, not one per day.
        Assert.DoesNotContain($"{RiderId}:{NotificationTemplate.RideRequestMatchedRider}", notifications.Sent);
    }

    [Fact]
    public async Task Only_the_chosen_days_are_taken()
    {
        var (uow, _, first) = await RiderCommute();
        var monday = RecurrenceRules.Flag(DayOfWeek.Monday);
        var proposal = await Make.Series(uow, DriverId).Propose(first.Id, Offer(days: monday));

        await Make.Series(uow, RiderId).Accept(proposal.Data!.Id);

        var matched = uow.Store<RideRequest>().Where(r => r.Status == RideRequestStatus.Matched).ToList();
        Assert.NotEmpty(matched);
        Assert.All(matched, r => Assert.Equal(DayOfWeek.Monday, r.OccurrenceDate!.Value.DayOfWeek));
        Assert.Contains(uow.Store<RideRequest>(), r => r.Status == RideRequestStatus.Open);
    }

    [Fact]
    public async Task A_day_the_driver_is_busy_stays_on_the_board_and_both_hear()
    {
        var (uow, _, first) = await RiderCommute();
        var clash = Build.Trip(99, DriverId, vehicleId: 1);
        clash.DepartAt = first.DepartAt;
        uow.Store<Trip>().Add(clash);
        var proposal = await Make.Series(uow, DriverId).Propose(first.Id, Offer());
        var notifications = new FakeNotificationService();

        var res = await Make.Series(uow, RiderId, notifications).Accept(proposal.Data!.Id);

        var day = res.Data!.Days.Single(d => d.Date == first.OccurrenceDate);
        Assert.Null(day.TripId);
        Assert.Equal(nameof(ErrorCode.DriverTripTimeConflict), day.Refusal!.Name);
        Assert.Equal(RideRequestStatus.Open, first.Status);
        Assert.Contains($"{RiderId}:{NotificationTemplate.SeriesDayOpenRider}", notifications.Sent);
    }

    [Fact]
    public async Task With_no_decision_time_the_offer_is_taken_at_once()
    {
        var config = new FakeAppConfigurationService { SeriesDecisionHours = 0 };
        var (uow, _, first) = await RiderCommute(config);

        var res = await Make.Series(uow, DriverId, config: config).Propose(first.Id, Offer());

        Assert.Equal(SeriesStatus.Active, res.Data!.Status);
        Assert.Equal(RideRequestStatus.Matched, first.Status);
    }

    [Fact]
    public async Task An_unanswered_offer_is_decided_at_its_deadline_for_the_most_reliable_driver()
    {
        var (uow, _, first) = await RiderCommute();
        await Make.Series(uow, DriverId).Propose(first.Id, Offer(1m));
        var steady = await Make.Series(uow, OtherDriverId).Propose(first.Id, Offer(3m));

        // The cheaper driver cancels a lot; the dearer one never has.
        var flaky = uow.Store<User>().Single(u => u.Id == DriverId);
        flaky.TripsAsDriver = 2;
        flaky.DriverCancellations = 3;
        uow.Store<User>().Single(u => u.Id == OtherDriverId).TripsAsDriver = 10;
        foreach (var c in uow.Store<SeriesCommitment>()) c.DecideAt = DateTime.UtcNow.AddMinutes(-1);

        var decided = await Make.Series(uow, 0).DecideDue();

        Assert.Equal(1, decided);
        Assert.Equal(SeriesStatus.Active, uow.Store<SeriesCommitment>().Single(c => c.Id == steady.Data!.Id).Status);
        Assert.All(uow.Store<Trip>(), t => Assert.Equal(OtherDriverId, t.DriverId));
    }

    [Fact]
    public async Task A_new_day_of_an_accepted_series_goes_straight_to_the_driver()
    {
        var (uow, _, first) = await RiderCommute();
        var proposal = await Make.Series(uow, DriverId).Propose(first.Id, Offer());
        await Make.Series(uow, RiderId).Accept(proposal.Data!.Id);

        // Forget the last generated day, as if the horizon had just reached it.
        var last = uow.Store<RideRequest>().OrderBy(r => r.DepartAt).Last();
        var lastTrip = uow.Store<Trip>().Single(t => t.Id == last.MatchedTripId);
        uow.Store<Booking>().RemoveAll(b => b.TripId == lastTrip.Id);
        uow.Store<Trip>().Remove(lastTrip);
        uow.Store<RideRequestParticipant>().RemoveAll(p => p.RideRequestId == last.Id);
        uow.Store<RideRequest>().Remove(last);
        uow.Store<TripSchedule>()[0].MaterialisedThrough = null;

        await Make.Schedules(uow, RiderId).MaterialiseDue();

        var regenerated = uow.Store<RideRequest>().Single(r => r.OccurrenceDate == last.OccurrenceDate);
        Assert.Equal(RideRequestStatus.Matched, regenerated.Status);
        var trip = uow.Store<Trip>().Single(t => t.Id == regenerated.MatchedTripId);
        Assert.Equal(DriverId, trip.DriverId);
        Assert.Equal(proposal.Data.Id, trip.SeriesCommitmentId);
    }

    // ── A rider books a driver's series ──────────────────────────────────────

    [Fact]
    public async Task Joining_a_series_books_every_upcoming_day_once()
    {
        var (uow, _, first) = await DriverCommute();
        var notifications = new FakeNotificationService();

        var res = await Make.Series(uow, RiderId, notifications).Join(first.Id,
            new JoinSeriesInput { Seats = 2, AcceptSharedRide = true });

        Assert.True(res.Success);
        var trips = uow.Store<Trip>();
        Assert.Equal(trips.Count, res.Data!.Days.Count(d => d.BookingId != null));
        Assert.All(trips, t =>
        {
            var seat = Assert.Single(uow.Store<Booking>(), b => b.TripId == t.Id);
            Assert.Equal(RiderId, seat.RiderId);
            Assert.Equal(2, seat.Seats);
            Assert.Equal(res.Data.Series.Id, seat.SeriesCommitmentId);
            Assert.Equal(1, t.SeatsLeft);
        });
        Assert.Contains($"{DriverId}:{NotificationTemplate.SeriesRiderJoinedDriver}", notifications.Sent);
        Assert.DoesNotContain($"{RiderId}:{NotificationTemplate.BookingConfirmedRider}", notifications.Sent);

        var again = await Make.Series(uow, RiderId).Join(first.Id, new JoinSeriesInput());
        Assert.Equal(ErrorCode.SeriesAlreadyCommitted, again.ErrorCode);
    }

    [Fact]
    public async Task A_full_day_is_reported_and_the_rest_are_booked()
    {
        var (uow, _, first) = await DriverCommute();
        first.SeatsLeft = 0;

        var res = await Make.Series(uow, RiderId).Join(first.Id, new JoinSeriesInput());

        var day = res.Data!.Days.Single(d => d.TripId == first.Id);
        Assert.Null(day.BookingId);
        Assert.Equal(nameof(ErrorCode.NoSeatsLeft), day.Refusal!.Name);
        Assert.True(res.Data.Days.Count(d => d.BookingId != null) > 0);
    }

    [Fact]
    public async Task A_one_off_trip_cannot_be_booked_as_a_series()
    {
        var uow = Scene();
        uow.Store<Trip>().Add(Build.Trip(50, DriverId, 1));

        var res = await Make.Series(uow, RiderId).Join(50, new JoinSeriesInput());

        Assert.Equal(ErrorCode.NotRecurring, res.ErrorCode);
    }

    [Fact]
    public async Task A_new_day_of_a_booked_series_gets_the_riders_seat()
    {
        var (uow, _, first) = await DriverCommute();
        var joined = await Make.Series(uow, RiderId).Join(first.Id, new JoinSeriesInput());

        var last = uow.Store<Trip>().OrderBy(t => t.DepartAt).Last();
        uow.Store<Booking>().RemoveAll(b => b.TripId == last.Id);
        uow.Store<Trip>().Remove(last);
        uow.Store<TripSchedule>()[0].MaterialisedThrough = null;

        await Make.Schedules(uow, DriverId).MaterialiseDue();

        var regenerated = uow.Store<Trip>().Single(t => t.OccurrenceDate == last.OccurrenceDate);
        var seat = Assert.Single(uow.Store<Booking>(), b => b.TripId == regenerated.Id);
        Assert.Equal(joined.Data!.Series.Id, seat.SeriesCommitmentId);
    }

    [Fact]
    public async Task Giving_back_a_series_seat_inside_a_day_is_late()
    {
        var (uow, _, first) = await DriverCommute();
        await Make.Series(uow, RiderId).Join(first.Id, new JoinSeriesInput());
        first.DepartAt = DateTime.UtcNow.AddHours(10);
        var seat = uow.Store<Booking>().Single(b => b.TripId == first.Id);

        await Make.Bookings(uow, RiderId).Cancel(seat.Id);

        var entry = Assert.Single(uow.Store<ReliabilityEvent>());
        Assert.Equal(ReliabilityEventKind.RiderLateCancel, entry.Kind);
    }

    // ── Skipping one day ─────────────────────────────────────────────────────

    private static async Task<(FakeUnitOfWork Uow, SeriesCommitment Series)> DrivenSeries(
        FakeAppConfigurationService? config = null)
    {
        var (uow, _, first) = await RiderCommute(config);
        var proposal = await Make.Series(uow, DriverId, config: config).Propose(first.Id, Offer());
        await Make.Series(uow, RiderId, config: config).Accept(proposal.Data!.Id);
        return (uow, uow.Store<SeriesCommitment>().Single(c => c.Id == proposal.Data.Id));
    }

    [Fact]
    public async Task Skipping_a_day_with_a_days_notice_costs_nothing_and_names_the_day()
    {
        var (uow, _) = await DrivenSeries();
        var day = uow.Store<Trip>().OrderBy(t => t.DepartAt).Last();
        var notifications = new FakeNotificationService();

        var res = await Make.Trips(uow, DriverId, notifications)
            .Cancel(day.Id, new CancelTripInput { Reason = CancelReason.Personal });

        Assert.True(res.Success);
        var entry = Assert.Single(uow.Store<ReliabilityEvent>());
        Assert.Equal(ReliabilityEventKind.SeriesSkip, entry.Kind);
        Assert.Equal(0, entry.Points);
    }

    [Fact]
    public async Task Skipping_a_day_at_short_notice_is_late()
    {
        var (uow, _) = await DrivenSeries();
        var day = uow.Store<Trip>().OrderBy(t => t.DepartAt).First();
        day.DepartAt = DateTime.UtcNow.AddHours(5);

        await Make.Trips(uow, DriverId).Cancel(day.Id, new CancelTripInput { Reason = CancelReason.Personal });

        Assert.Equal(ReliabilityEventKind.LateCancel, Assert.Single(uow.Store<ReliabilityEvent>()).Kind);
    }

    [Fact]
    public async Task Skips_beyond_the_free_allowance_count()
    {
        var config = new FakeAppConfigurationService { SeriesFreeSkipsPerWindow = 1 };
        var (uow, _) = await DrivenSeries(config);
        var days = uow.Store<Trip>().OrderByDescending(t => t.DepartAt).Take(2).ToList();

        foreach (var day in days)
            await Make.Trips(uow, DriverId, config: config)
                .Cancel(day.Id, new CancelTripInput { Reason = CancelReason.Personal });

        var kinds = uow.Store<ReliabilityEvent>().Select(e => e.Kind).ToList();
        Assert.Equal([ReliabilityEventKind.SeriesSkip, ReliabilityEventKind.Cancel], kinds);
    }

    // ── Ending ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ending_with_notice_keeps_the_notice_days_and_costs_nothing()
    {
        var (uow, series) = await DrivenSeries();
        var preview = await Make.Series(uow, DriverId).EndPreview(series.Id);
        var noticeEnd = preview.Data!.NoticeEnd;

        var res = await Make.Series(uow, DriverId).End(series.Id, new EndSeriesInput());

        Assert.True(res.Success);
        Assert.Equal(noticeEnd, res.Data!.Until);
        Assert.All(uow.Store<Trip>(), t =>
            Assert.Equal(t.OccurrenceDate <= noticeEnd ? TripStatus.Posted : TripStatus.Cancelled, t.Status));
        Assert.Contains(uow.Store<Trip>(), t => t.Status == TripStatus.Cancelled);
        Assert.Empty(uow.Store<ReliabilityEvent>());
        Assert.Equal(preview.Data.DaysDroppedWithNotice,
            uow.Store<Trip>().Count(t => t.Status == TripStatus.Cancelled));
    }

    [Fact]
    public async Task Ending_at_once_costs_a_point_for_each_day_inside_the_notice()
    {
        var (uow, series) = await DrivenSeries();
        var preview = (await Make.Series(uow, DriverId).EndPreview(series.Id)).Data!;

        var noReason = await Make.Series(uow, DriverId).End(series.Id, new EndSeriesInput { Immediately = true });
        Assert.Equal(ErrorCode.CancelReasonRequired, noReason.ErrorCode);

        var res = await Make.Series(uow, DriverId).End(series.Id,
            new EndSeriesInput { Immediately = true, Reason = CancelReason.Personal });

        Assert.Equal(SeriesStatus.Ended, res.Data!.Status);
        Assert.All(uow.Store<Trip>(), t => Assert.Equal(TripStatus.Cancelled, t.Status));
        var points = uow.Store<ReliabilityEvent>().Where(e => e.Kind == ReliabilityEventKind.SeriesEndShortNotice).ToList();
        Assert.Equal(preview.PointsNow, points.Count);
        Assert.True(points.Count > 0);
    }

    [Fact]
    public async Task The_rider_releasing_their_driver_costs_the_driver_nothing()
    {
        var (uow, series) = await DrivenSeries();
        var notifications = new FakeNotificationService();

        var res = await Make.Series(uow, RiderId, notifications).End(series.Id,
            new EndSeriesInput { Immediately = true });

        Assert.True(res.Success);
        Assert.Empty(uow.Store<ReliabilityEvent>());
        Assert.Contains($"{DriverId}:{NotificationTemplate.SeriesEnded}", notifications.Sent);
    }

    [Fact]
    public async Task A_rider_ending_their_booking_with_notice_keeps_the_near_days()
    {
        var (uow, _, first) = await DriverCommute();
        var joined = await Make.Series(uow, RiderId).Join(first.Id, new JoinSeriesInput());

        var res = await Make.Series(uow, RiderId).End(joined.Data!.Series.Id, new EndSeriesInput());

        var noticeEnd = res.Data!.Until!.Value;
        Assert.All(uow.Store<Booking>(), b =>
        {
            var trip = uow.Store<Trip>().Single(t => t.Id == b.TripId);
            Assert.Equal(trip.OccurrenceDate <= noticeEnd ? BookingStatus.Confirmed : BookingStatus.Cancelled, b.Status);
        });
        Assert.Contains(uow.Store<Booking>(), b => b.Status == BookingStatus.Cancelled);
    }

    [Fact]
    public async Task The_driver_cannot_end_a_riders_booking_on_their_schedule()
    {
        var (uow, _, first) = await DriverCommute();
        var joined = await Make.Series(uow, RiderId).Join(first.Id, new JoinSeriesInput());

        var res = await Make.Series(uow, DriverId).End(joined.Data!.Series.Id, new EndSeriesInput());

        Assert.Equal(ErrorCode.SeriesNotAllowed, res.ErrorCode);
    }

    [Fact]
    public async Task Deleting_the_schedule_releases_everyone_on_it()
    {
        var (uow, series) = await DrivenSeries();
        var notifications = new FakeNotificationService();

        await Make.Schedules(uow, RiderId, notifications: notifications).Delete(series.ScheduleId);

        Assert.Equal(SeriesStatus.Ended, series.Status);
        Assert.Contains($"{DriverId}:{NotificationTemplate.SeriesEnded}", notifications.Sent);
        Assert.Empty(uow.Store<ReliabilityEvent>());
    }

    [Fact]
    public async Task A_finished_commitment_is_closed()
    {
        var (uow, series) = await DrivenSeries();
        series.Until = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        Assert.Equal(1, await Make.Series(uow, 0).CloseFinished());
        Assert.Equal(SeriesStatus.Ended, series.Status);
    }

    // ── The week ahead, and the card ─────────────────────────────────────────

    [Fact]
    public async Task The_week_ahead_goes_out_once_on_the_summary_day()
    {
        var config = new FakeAppConfigurationService { SeriesSummaryDay = (int)DateTime.UtcNow.DayOfWeek };
        var (uow, _) = await DrivenSeries(config);
        var notifications = new FakeNotificationService();

        Assert.Equal(1, await Make.Series(uow, 0, notifications, config).SendWeeklySummaries());
        Assert.Contains($"{DriverId}:{NotificationTemplate.SeriesWeeklySummary}", notifications.Sent);
        Assert.Contains($"{RiderId}:{NotificationTemplate.SeriesWeeklySummary}", notifications.Sent);
        Assert.Equal(0, await Make.Series(uow, 0, notifications, config).SendWeeklySummaries());
    }

    [Fact]
    public async Task A_recurring_card_says_so_and_shows_the_callers_offer()
    {
        var (uow, scheduleId, first) = await RiderCommute();
        var proposal = await Make.Series(uow, DriverId).Propose(first.Id, Offer());
        var row = new RideRequestRow { Id = first.Id, ScheduleId = scheduleId };

        await Make.SeriesInfo(uow, DriverId).Decorate([row]);

        Assert.NotNull(row.Series);
        Assert.Equal(Recurrence.Daily, row.Series!.Recurrence);
        Assert.Equal(ActiveRole.Rider, row.Series.OwnerRole);
        Assert.True(row.Series.UpcomingDays > 1);
        Assert.Equal(proposal.Data!.Id, row.Series.MySeriesId);
        Assert.Equal(1, row.Series.ProposalCount);
        Assert.False(row.Series.HasDriver);
    }

    [Fact]
    public async Task A_recurring_only_alert_ignores_one_off_requests()
    {
        var uow = Scene();
        uow.Store<DemandAlert>().Add(new DemandAlert
        {
            Id = 1, DriverId = DriverId, MinSeats = 1, RadiusMeters = 3000, RecurringOnly = true,
            OriginAddress = "A", Origin = Wanes.Shareds.Extensions.GeoFactory.Point(31.95, 35.92),
            DestinationAddress = "B", Destination = Wanes.Shareds.Extensions.GeoFactory.Point(32.01, 35.87),
        });
        var oneOff = Build.Demand(uow, 40, RiderId);

        Assert.Equal(0, await Make.Alerts(uow, DriverId).Match(oneOff));

        oneOff.ScheduleId = 1;
        Assert.Equal(1, await Make.Alerts(uow, DriverId).Match(oneOff));
    }
}
