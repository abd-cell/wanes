using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Bookings.Models;
using Wanes.Areas.Services.Trips.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// v2's three dimensions: a trip's lifecycle, its confirmation and its capacity
/// are separate questions, and none of them is allowed to answer another.
///
/// The two rules with teeth are here because both were wrong when they lived in
/// one status column. Capacity moves **both ways** — a cancelled seat unfills a
/// trip — so it can never be a rung on a ladder. Confirmation moves **one way**
/// — once riders have been told their ride is on, a stranger's change of mind
/// must not take it away — so it cannot be re-derived from the live seat count.
/// A single enum gets one of the two wrong whichever way it is written.
/// </summary>
public class TripStateDimensionsTests
{
    private const int DriverId = 2;
    private const int RiderId = 5;
    private const int OtherRiderId = 6;

    /// <summary>A two-seat trip that confirms as soon as both seats are taken.</summary>
    private static FakeUnitOfWork Scene(int seats = 2, int minSeats = 2)
    {
        var uow = new FakeUnitOfWork();
        var driver = Build.Driver(DriverId);
        uow.Store<User>().Add(driver);
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<User>().Add(Build.Rider(OtherRiderId));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId));

        var trip = Build.Trip(10, driverId: DriverId, vehicleId: 1, seatsTotal: seats);
        trip.Driver = driver;
        trip.DepartAt = DateTime.UtcNow.AddHours(6);
        trip.MinSeatsToConfirm = minSeats;
        uow.Store<Trip>().Add(trip);
        return uow;
    }

    private static async Task<BaseResponse<BookingOutput>> Book(FakeUnitOfWork uow, int riderId,
        int seats = 1) =>
        await Make.Bookings(uow, riderId).Create(new CreateBookingInput { TripId = 10, Seats = seats });

    // ── Capacity ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_trip_with_no_seats_left_is_full_without_leaving_Posted()
    {
        var uow = Scene();

        await Book(uow, RiderId);
        await Book(uow, OtherRiderId);

        var trip = uow.Store<Trip>()[0];
        Assert.Equal(0, trip.SeatsLeft);
        Assert.True(trip.IsFull);

        // The lifecycle has not moved: the car has not gone anywhere, and a
        // driver looking at their day sees a trip that is still waiting to go.
        Assert.Equal(TripStatus.Posted, trip.Status);
    }

    [Fact]
    public async Task A_cancelled_seat_unfills_the_trip()
    {
        var uow = Scene();
        await Book(uow, RiderId);
        var second = await Book(uow, OtherRiderId);
        Assert.True(uow.Store<Trip>()[0].IsFull);

        await Make.Bookings(uow, OtherRiderId).Cancel(second.Data!.Id);

        // This is the direction a status ladder cannot travel, which is the whole
        // argument for capacity being derived from the seats and stored nowhere.
        var trip = uow.Store<Trip>()[0];
        Assert.False(trip.IsFull);
        Assert.Equal(TripStatus.Posted, trip.Status);
    }

    // ── Confirmation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_confirmed_trip_stays_confirmed_when_a_rider_leaves()
    {
        var uow = Scene(seats: 4, minSeats: 2);
        await Book(uow, RiderId);
        var second = await Book(uow, OtherRiderId);

        var trip = uow.Store<Trip>()[0];
        Assert.True(trip.IsConfirmed);

        await Make.Bookings(uow, OtherRiderId).Cancel(second.Data!.Id);

        trip = uow.Store<Trip>()[0];

        // Back under the threshold, and still confirmed. The rider who stayed was
        // told their ride is on; somebody else changing their mind is not allowed
        // to un-tell them. The freed seat goes back on sale and that is all.
        Assert.True(trip.IsConfirmed);
        Assert.False(TripConfirmationRules.IsGathering(
            trip.Status, trip.MinSeatsToConfirm,
            TripConfirmationRules.HeldSeats(uow.Store<Booking>()), trip.ConfirmedAt));
        Assert.Equal(3, trip.SeatsLeft);
        Assert.Equal(BookingStatus.Confirmed,
            uow.Store<Booking>().Single(b => b.RiderId == RiderId).Status);
    }

    [Fact]
    public async Task Confirmation_is_stamped_once_however_many_seats_cross_the_threshold()
    {
        var uow = Scene(seats: 4, minSeats: 2);
        await Book(uow, RiderId);
        await Book(uow, OtherRiderId);

        var stampedAt = uow.Store<Trip>()[0].ConfirmedAt;
        Assert.NotNull(stampedAt);

        // A third seat is not a second confirmation. If the stamp moved, every
        // retry and every late booking would be a fresh "your trip is confirmed"
        // to riders who have known for an hour.
        uow.Store<User>().Add(Build.Rider(7));
        await Book(uow, 7);

        Assert.Equal(stampedAt, uow.Store<Trip>()[0].ConfirmedAt);
    }

    [Fact]
    public async Task A_gathering_trip_is_not_confirmed_and_says_so_on_the_wire()
    {
        var uow = Scene(seats: 4, minSeats: 3);
        await Book(uow, RiderId);

        var trip = uow.Store<Trip>()[0];
        var output = new TripOutput(trip, trip.Driver);

        Assert.True(output.IsGathering);
        Assert.False(output.IsConfirmed);
        Assert.False(output.IsFull);
    }

    // ── The seeded threshold ─────────────────────────────────────────────────

    [Fact]
    public async Task A_driver_who_names_no_threshold_gets_the_marketplace_default()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(DriverId));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId, capacity: 4));

        var res = await Make.Trips(uow, DriverId).Create(new CreateTripInput
        {
            VehicleId = 1,
            Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
            Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
            DepartAt = DateTime.UtcNow.AddHours(3),
            SeatsTotal = 4,
        });

        Assert.True(res.Success);

        // Silence is not "one passenger will do" — it is "I have not thought
        // about it", and the marketplace has.
        Assert.Equal(TripConfirmationRules.DefaultMinimumPassengers,
            uow.Store<Trip>()[0].MinSeatsToConfirm);
    }

    [Fact]
    public async Task A_threshold_the_driver_did_name_is_theirs()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(DriverId));
        uow.Store<Vehicle>().Add(Build.Vehicle(1, userId: DriverId, capacity: 4));

        var res = await Make.Trips(uow, DriverId).Create(new CreateTripInput
        {
            VehicleId = 1,
            Origin = new GeoPoint { Lat = 31.95, Lng = 35.92, Address = "A" },
            Destination = new GeoPoint { Lat = 32.01, Lng = 35.87, Address = "B" },
            DepartAt = DateTime.UtcNow.AddHours(3),
            SeatsTotal = 4,
            MinSeatsToConfirm = TripConfirmationRules.NoThreshold,
        });

        Assert.True(res.Success);
        Assert.Equal(TripConfirmationRules.NoThreshold, uow.Store<Trip>()[0].MinSeatsToConfirm);
    }
}
