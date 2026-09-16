using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Bookings.Models;
using Wanes.Areas.Services.Search.Models;
using Wanes.Areas.Services.Trips.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// Who may share a car with whom.
///
/// Conditions run both ways, but they are not the same kind of thing. The
/// driver's condition on their riders is a *rule*: search filters on it and
/// booking refuses on it, because a filtered list is a convenience and an API
/// that trusted it would let a stale screen book past the rule. The rider's
/// condition on their driver is a *preference*: it travels with one search and
/// shapes what that search shows. The enforced version of it lives on a rider's
/// own posting, where a driver who does not meet it cannot claim.
///
/// The refusals are deliberately two different codes. A missing profile field
/// is fixable by the rider in thirty seconds; a condition they do not meet is
/// not fixable at all, and a client that cannot tell them apart either nags
/// people for no reason or leaves a fixable refusal looking final.
/// </summary>
public class RideConditionTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;

    private static readonly DateTime Departure = DateTime.UtcNow.AddHours(3);

    private static FakeUnitOfWork Scene(Gender driverGender = Gender.Male,
        Gender riderGender = Gender.Female, int riderAge = 30)
    {
        var uow = new FakeUnitOfWork();

        var driver = Build.Driver(DriverId);
        driver.Gender = driverGender;
        uow.Store<User>().Add(driver);

        var rider = Build.Rider(RiderId);
        rider.Gender = riderGender;
        rider.DateOfBirth = DateTime.UtcNow.AddYears(-riderAge).AddDays(-1);
        uow.Store<User>().Add(rider);

        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));

        var trip = Build.Trip(10, driverId: DriverId, vehicleId: 1, seatsTotal: 3);
        trip.Driver = driver;
        trip.DepartAt = Departure;
        uow.Store<Trip>().Add(trip);
        return uow;
    }

    private static Trip TheTrip(FakeUnitOfWork uow) => uow.Store<Trip>()[0];

    private static async Task<BaseResponse<BookingOutput>> Book(FakeUnitOfWork uow) =>
        await Make.Bookings(uow, RiderId).Create(new CreateBookingInput { TripId = 10, Seats = 1 });

    private static async Task<SearchResult> Search(FakeUnitOfWork uow,
        GenderPolicy wantedDriver = GenderPolicy.Any) =>
        (await Make.Search(uow, RiderId).Search(new SearchInput
        {
            Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
            Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
            When = Departure,
            Seats = 1,
            DriverGenderPolicy = wantedDriver,
        })).Data!;

    // ── The driver's conditions on their riders ──────────────────────────────

    [Fact]
    public async Task A_rider_the_trip_admits_may_book_it()
    {
        var uow = Scene(riderGender: Gender.Female);
        TheTrip(uow).GenderPolicy = GenderPolicy.FemaleOnly;

        Assert.True((await Book(uow)).Success);
    }

    [Fact]
    public async Task A_rider_the_trip_excludes_is_refused()
    {
        var uow = Scene(riderGender: Gender.Male);
        TheTrip(uow).GenderPolicy = GenderPolicy.FemaleOnly;

        var res = await Book(uow);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.RiderNotEligible, res.ErrorCode);
    }

    [Fact]
    public async Task A_restriction_needs_the_profile_field_it_checks()
    {
        var uow = Scene(riderGender: Gender.Unspecified);
        TheTrip(uow).GenderPolicy = GenderPolicy.FemaleOnly;

        var res = await Book(uow);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.RiderProfileIncomplete, res.ErrorCode);
    }

    [Fact]
    public async Task An_age_bound_is_checked_against_the_riders_own_age()
    {
        var uow = Scene(riderAge: 19);
        TheTrip(uow).MinAge = 25;

        var res = await Book(uow);

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.RiderNotEligible, res.ErrorCode);
    }

    [Fact]
    public async Task An_age_bound_with_no_date_of_birth_to_check_refuses_as_incomplete()
    {
        var uow = Scene();
        uow.Store<User>().First(u => u.Id == RiderId).DateOfBirth = null;
        TheTrip(uow).MinAge = 25;

        var res = await Book(uow);

        Assert.Equal(ErrorCode.RiderProfileIncomplete, res.ErrorCode);
    }

    [Fact]
    public async Task An_ineligible_trip_is_filtered_out_of_search_rather_than_shown()
    {
        var uow = Scene(riderGender: Gender.Male);
        TheTrip(uow).GenderPolicy = GenderPolicy.FemaleOnly;

        // A result the rider cannot book is a bug in the list; showing it greyed
        // out leaks a policy they cannot act on either way.
        Assert.Empty((await Search(uow)).Matches);
    }

    [Fact]
    public async Task A_trip_that_admits_the_rider_is_still_offered()
    {
        var uow = Scene(riderGender: Gender.Female);
        TheTrip(uow).GenderPolicy = GenderPolicy.FemaleOnly;

        Assert.Single((await Search(uow)).Matches);
    }

    // ── The rider's condition on their driver ────────────────────────────────

    [Fact]
    public async Task A_rider_who_asked_for_a_female_driver_is_not_offered_a_male_one()
    {
        var uow = Scene(driverGender: Gender.Male);

        Assert.Empty((await Search(uow, GenderPolicy.FemaleOnly)).Matches);
    }

    [Fact]
    public async Task A_driver_who_meets_it_is_offered()
    {
        var uow = Scene(driverGender: Gender.Female);

        Assert.Single((await Search(uow, GenderPolicy.FemaleOnly)).Matches);
    }

    [Fact]
    public async Task The_driver_condition_shapes_the_search_rather_than_barring_the_booking()
    {
        // It travels with the search, not with the account, so it says what this
        // rider wants to be *shown* this time. A rider who searched for a woman
        // at the wheel and then deliberately opened a man's trip is not stopped:
        // the preference was theirs and so is the change of mind.
        //
        // The enforced version of this condition lives on a rider's own posting,
        // where a driver who does not meet it cannot claim (see RiderTripTests).
        var uow = Scene(driverGender: Gender.Male);

        Assert.Empty((await Search(uow, GenderPolicy.FemaleOnly)).Matches);
        Assert.True((await Book(uow)).Success);
    }

    // ── Posting a trip with conditions ───────────────────────────────────────

    [Fact]
    public async Task A_driver_posts_their_conditions_with_the_trip()
    {
        var uow = Scene();

        var res = await Make.Trips(uow, DriverId).Create(new CreateTripInput
        {
            VehicleId = 1,
            Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
            Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
            DepartAt = DateTime.UtcNow.AddHours(9),
            SeatsTotal = 3,
            MinSeatsToConfirm = 2,
            GenderPolicy = GenderPolicy.FemaleOnly,
            MinAge = 21,
        });

        Assert.True(res.Success);
        Assert.Equal(GenderPolicy.FemaleOnly, res.Data!.GenderPolicy);
        Assert.Equal(2, res.Data.MinSeatsToConfirm);
        Assert.Equal(21, res.Data.MinAge);
        Assert.True(res.Data.IsGathering);
    }

    [Fact]
    public async Task A_threshold_the_trip_cannot_reach_is_refused()
    {
        var uow = Scene();

        var res = await Make.Trips(uow, DriverId).Create(new CreateTripInput
        {
            VehicleId = 1,
            Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
            Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
            DepartAt = DateTime.UtcNow.AddHours(9),
            SeatsTotal = 2,
            MinSeatsToConfirm = 3,
        });

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.MinSeatsExceedTotal, res.ErrorCode);
    }

    // ── The rules themselves ─────────────────────────────────────────────────

    [Fact]
    public void A_policy_admits_the_gender_it_names_and_nobody_who_has_not_said()
    {
        Assert.True(RiderEligibilityRules.Admits(GenderPolicy.Any, Gender.Unspecified));
        Assert.True(RiderEligibilityRules.Admits(GenderPolicy.FemaleOnly, Gender.Female));
        Assert.False(RiderEligibilityRules.Admits(GenderPolicy.FemaleOnly, Gender.Male));
        Assert.False(RiderEligibilityRules.Admits(GenderPolicy.FemaleOnly, Gender.Unspecified));
    }

    [Fact]
    public void Age_is_taken_at_the_departure_not_at_the_booking()
    {
        var departAt = new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc);
        var birthday = new DateTime(2006, 11, 30, 0, 0, 0, DateTimeKind.Utc);

        // Twenty by the day they travel, which is the day the condition is about.
        Assert.Equal(20, RiderEligibilityRules.AgeAt(birthday, departAt));
        Assert.Equal(19, RiderEligibilityRules.AgeAt(birthday, departAt.AddMonths(-2)));
    }

    [Fact]
    public void An_age_bound_with_nothing_to_check_against_is_not_satisfied()
    {
        Assert.True(RiderEligibilityRules.WithinAge(null, null, null));
        Assert.False(RiderEligibilityRules.WithinAge(null, 18, null));
        Assert.True(RiderEligibilityRules.WithinAge(30, 18, 40));
        Assert.False(RiderEligibilityRules.WithinAge(41, 18, 40));
    }

    [Fact]
    public void Tightening_takes_the_stricter_of_two_condition_sets()
    {
        var pool = new RideConditions(GenderPolicy.Any, 18, 60);
        var joiner = new RideConditions(GenderPolicy.FemaleOnly, 25, 50);

        var tightened = pool.Tighten(joiner);

        Assert.Equal(GenderPolicy.FemaleOnly, tightened.GenderPolicy);
        Assert.Equal(25, tightened.MinAge);
        Assert.Equal(50, tightened.MaxAge);
    }
}
