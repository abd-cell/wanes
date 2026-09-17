using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.Schedules;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Schedules.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// Recurring postings, and the pass that turns them into real rows.
///
/// The invariant worth defending: <b>a schedule never matches anything</b>. It
/// generates ordinary trips and ordinary postings, and search, booking,
/// claiming and the availability rules never learn recurrence exists. Which is
/// why almost every test here asserts about the rows a schedule produced rather
/// than about the schedule.
/// </summary>
public class ScheduleTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;

    private static FakeUnitOfWork Scene()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(DriverId));
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));
        return uow;
    }

    /// <summary>A daily schedule at a fixed UTC hour, starting today.</summary>
    private static TripScheduleInput Daily(ActiveRole role, TimeOnly? at = null) => new()
    {
        OwnerRole = role,
        Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
        Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
        Recurrence = Recurrence.Daily,
        TimeOfDay = at ?? new TimeOnly(7, 30),
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
        Seats = 2,
        PricePerSeat = role == ActiveRole.Driver ? 3m : null,
        VehicleId = role == ActiveRole.Driver ? 1 : null,
    };

    // ── Writing one ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_schedule_previews_the_departures_it_will_produce()
    {
        var uow = Scene();

        var res = await Make.Schedules(uow, DriverId).Create(Daily(ActiveRole.Driver));

        Assert.True(res.Success);
        Assert.NotEmpty(res.Data!.NextDepartures);
        Assert.All(res.Data.NextDepartures, d => Assert.True(d > DateTime.UtcNow));
    }

    [Fact]
    public async Task A_weekly_schedule_with_no_day_chosen_would_never_fire()
    {
        var uow = Scene();
        var input = Daily(ActiveRole.Driver);
        input.Recurrence = Recurrence.Weekly;
        input.DaysOfWeek = WeekDays.None;

        var res = await Make.Schedules(uow, DriverId).Create(input);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.ScheduleHasNoOccurrences, res.ErrorCode);
    }

    [Fact]
    public async Task A_drivers_schedule_needs_a_car()
    {
        var uow = Scene();
        uow.Store<Vehicle>().Clear();

        var res = await Make.Schedules(uow, DriverId).Create(Daily(ActiveRole.Driver));

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.VehicleNotFound, res.ErrorCode);
    }

    [Fact]
    public async Task A_riders_schedule_carries_no_price_or_car()
    {
        var uow = Scene();
        var input = Daily(ActiveRole.Rider);
        input.PricePerSeat = 5m;
        input.VehicleId = 1;

        var res = await Make.Schedules(uow, RiderId).Create(input);

        Assert.True(res.Success);
        Assert.Null(res.Data!.PricePerSeat);
        Assert.Null(res.Data.VehicleId);
    }

    // ── Materialising ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_drivers_schedule_produces_ordinary_trips()
    {
        var uow = Scene();

        // Writing the schedule generates its first fortnight straight away;
        // the worker's next pass finds nothing left to do.
        var created = await Make.Schedules(uow, DriverId).Create(Daily(ActiveRole.Driver));
        var written = await Make.Schedules(uow, DriverId).MaterialiseDue();

        Assert.True(created.Data!.Generated > 0);
        Assert.Equal(0, written);
        var trips = uow.Store<Trip>();
        Assert.Equal(created.Data.Generated, trips.Count);
        Assert.All(trips, t =>
        {
            Assert.Equal(DriverId, t.DriverId);
            Assert.Equal(TripStatus.Posted, t.Status);
            Assert.Equal(3m, t.PricePerSeat);
            Assert.Equal(2, t.SeatsTotal);
            Assert.NotNull(t.OccurrenceDate);
        });

        // A rolling fortnight, one trip a day, and never one in the past.
        Assert.True(trips.Count <= Areas.Domain.Schedules.RecurrenceRules.HorizonDays + 1);
        Assert.All(trips, t => Assert.True(t.DepartAt > DateTime.UtcNow));
    }

    [Fact]
    public async Task A_riders_schedule_produces_requests_with_their_seats_held()
    {
        var uow = Scene();
        await Make.Schedules(uow, RiderId).Create(Daily(ActiveRole.Rider));

        await Make.Schedules(uow, RiderId).MaterialiseDue();

        // Demand, not driverless trips: a schedule is a generator, and what it
        // generates for a rider is the same object a rider would have created.
        var requests = uow.Store<RideRequest>();
        Assert.NotEmpty(requests);
        Assert.Empty(uow.Store<Trip>());
        Assert.All(requests, r =>
        {
            Assert.Equal(RideRequestStatus.Open, r.Status);
            Assert.Equal(2, r.SeatsRequested);
            Assert.NotNull(r.OccurrenceDate);
        });

        // The owner's seat on each, which is what makes them pools rather than
        // empty rows.
        Assert.All(requests, r =>
        {
            var on = Assert.Single(uow.Store<RideRequestParticipant>(),
                p => p.RideRequestId == r.Id);
            Assert.Equal(RiderId, on.RiderId);
            Assert.Equal(RideRequestParticipantStatus.Active, on.Status);
        });
    }

    [Fact]
    public async Task A_second_pass_writes_nothing_twice()
    {
        var uow = Scene();
        await Make.Schedules(uow, DriverId).Create(Daily(ActiveRole.Driver));

        await Make.Schedules(uow, DriverId).MaterialiseDue();
        var before = uow.Store<Trip>().Count;
        var second = await Make.Schedules(uow, DriverId).MaterialiseDue();

        Assert.Equal(0, second);
        Assert.Equal(before, uow.Store<Trip>().Count);
    }

    [Fact]
    public async Task A_paused_schedule_stops_generating_and_leaves_what_exists()
    {
        var uow = Scene();
        var created = await Make.Schedules(uow, DriverId).Create(Daily(ActiveRole.Driver));
        await Make.Schedules(uow, DriverId).MaterialiseDue();
        var generated = uow.Store<Trip>().Count;

        var paused = Daily(ActiveRole.Driver);
        paused.IsPaused = true;
        await Make.Schedules(uow, DriverId).Update(created.Data!.Id, paused);

        // Reset the watermark so the only thing stopping generation is the pause.
        uow.Store<TripSchedule>()[0].MaterialisedThrough = null;

        Assert.Equal(0, await Make.Schedules(uow, DriverId).MaterialiseDue());
        Assert.Equal(generated, uow.Store<Trip>().Count);
    }

    [Fact]
    public async Task An_occurrence_that_clashes_with_the_drivers_own_trip_is_skipped()
    {
        var uow = Scene();

        // A trip already promised at the same hour tomorrow. A schedule cannot
        // pre-book a driver's calendar, so that date is skipped — and only that
        // date.
        var tomorrow = DateTime.UtcNow.Date.AddDays(1).AddHours(7).AddMinutes(30);
        var existing = Build.Trip(99, driverId: DriverId, vehicleId: 1);
        existing.DepartAt = tomorrow;
        uow.Store<Trip>().Add(existing);

        await Make.Schedules(uow, DriverId).Create(Daily(ActiveRole.Driver, new TimeOnly(7, 30)));
        await Make.Schedules(uow, DriverId).MaterialiseDue();

        var generated = uow.Store<Trip>().Where(t => t.ScheduleId != null).ToList();
        Assert.DoesNotContain(generated, t => t.DepartAt == tomorrow);
        Assert.NotEmpty(generated);
    }

    // ── Deleting ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deleting_a_schedule_calls_off_its_unbooked_future_trips()
    {
        var uow = Scene();
        var created = await Make.Schedules(uow, DriverId).Create(Daily(ActiveRole.Driver));
        await Make.Schedules(uow, DriverId).MaterialiseDue();

        // One of them has a rider on it.
        var booked = uow.Store<Trip>().First();
        uow.Store<Booking>().Add(new Booking
        {
            Id = 1, TripId = booked.Id, RiderId = RiderId, Seats = 1, Status = BookingStatus.Confirmed,
        });

        var res = await Make.Schedules(uow, DriverId).Delete(created.Data!.Id);

        Assert.True(res.Success);

        // The booked one still runs: it stopped being "part of a series" the
        // moment somebody took a seat on it.
        Assert.Equal(TripStatus.Posted, uow.Store<Trip>().First(t => t.Id == booked.Id).Status);
        Assert.All(uow.Store<Trip>().Where(t => t.Id != booked.Id),
            t => Assert.Equal(TripStatus.Cancelled, t.Status));
    }

    [Fact]
    public async Task A_deleted_schedule_generates_nothing_more()
    {
        var uow = Scene();
        var created = await Make.Schedules(uow, DriverId).Create(Daily(ActiveRole.Driver));
        await Make.Schedules(uow, DriverId).Delete(created.Data!.Id);

        Assert.Equal(0, await Make.Schedules(uow, DriverId).MaterialiseDue());
    }

    // ── The recurrence itself ────────────────────────────────────────────────

    [Fact]
    public void A_weekly_schedule_fires_only_on_its_chosen_days()
    {
        var from = new DateOnly(2026, 9, 7);   // Monday
        var to = new DateOnly(2026, 9, 13);

        var dates = Areas.Domain.Schedules.RecurrenceRules
            .Occurrences(Recurrence.Weekly, WeekDays.Monday | WeekDays.Wednesday, null, from, to)
            .ToList();

        Assert.Equal([new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 9)], dates);
    }

    [Fact]
    public void A_monthly_schedule_on_the_thirty_first_runs_on_the_last_day_of_short_months()
    {
        var february = new DateOnly(2026, 2, 1);

        var day = Areas.Domain.Schedules.RecurrenceRules.EffectiveDayOfMonth(31, february);
        var dates = Areas.Domain.Schedules.RecurrenceRules
            .Occurrences(Recurrence.Monthly, WeekDays.None, 31, february, new DateOnly(2026, 2, 28))
            .ToList();

        Assert.Equal(28, day);
        Assert.Equal([new DateOnly(2026, 2, 28)], dates);
    }

    [Fact]
    public void A_daily_schedule_fires_every_day_in_the_window()
    {
        var dates = Areas.Domain.Schedules.RecurrenceRules
            .Occurrences(Recurrence.Daily, WeekDays.None, null,
                new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 10))
            .ToList();

        Assert.Equal(4, dates.Count);
    }

    [Fact]
    public void A_wall_clock_time_becomes_the_instant_it_names_in_its_own_zone()
    {
        var zone = Areas.Domain.Schedules.RecurrenceRules.ZoneFor("Asia/Amman");
        var date = new DateOnly(2026, 1, 15);

        var utc = Areas.Domain.Schedules.RecurrenceRules.ToUtc(date, new TimeOnly(7, 30), zone);

        // Amman is UTC+3 in winter, so half seven local is half four UTC.
        Assert.Equal(new DateTime(2026, 1, 15, 4, 30, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void A_zone_this_machine_does_not_know_falls_back_to_utc()
    {
        // A trimmed time-zone database must not stop a schedule generating
        // trips: an offset that is wrong is visible and fixable, a worker that
        // dies is neither.
        var zone = Areas.Domain.Schedules.RecurrenceRules.ZoneFor("Mars/Olympus_Mons");

        Assert.Equal(TimeZoneInfo.Utc, zone);
    }

    [Theory]
    [InlineData("+03")]
    [InlineData("GMT+03:00")]
    [InlineData("UTC+3")]
    public void A_phones_offset_is_read_as_a_fixed_zone(string id)
    {
        // What a phone reports as its zone name. Read as UTC, a 07:30 commute
        // would leave at 10:30.
        var zone = Areas.Domain.Schedules.RecurrenceRules.ZoneFor(id);
        var departs = Areas.Domain.Schedules.RecurrenceRules.ToUtc(
            new DateOnly(2026, 9, 20), new TimeOnly(7, 30), zone);

        Assert.Equal(new DateTime(2026, 9, 20, 4, 30, 0), departs);
    }

    [Fact]
    public void An_empty_window_produces_nothing_rather_than_looping()
    {
        var dates = Areas.Domain.Schedules.RecurrenceRules
            .Occurrences(Recurrence.Daily, WeekDays.None, null,
                new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 7))
            .ToList();

        Assert.Empty(dates);
    }
}
