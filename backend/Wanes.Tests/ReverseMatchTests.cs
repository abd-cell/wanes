using Microsoft.Extensions.Logging.Abstractions;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Notifications;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Users.Availability;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.SSE;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// The reverse match: a trip appears, and the riders already sitting on an open
/// hail along that route are told about it.
///
/// It runs from both routes into a new trip — a driver posting one, and a driver
/// accepting a hail — so it lives on the notification service rather than in
/// either caller. What is pinned here is who it picks: matching the wrong riders
/// is a push to someone whose journey it does not serve.
///
/// The radius half of the filter is not covered here and cannot be: the doubles
/// run NetTopologySuite in memory, where `Distance` is planar and answers in
/// degrees, while the real query runs against SQL Server `geography` and answers
/// in metres. A radius assertion would pass or fail for reasons unrelated to the
/// rule. Everything the shim *can* model honestly is below.
/// </summary>
public class ReverseMatchTests
{
    private const int DriverId = 2;

    private static NotificationService Service(FakeUnitOfWork uow) =>
        new(uow, new FakeAuditService(), new FakeSecurityManager(DriverId), new FakeFcmSender(),
            new SseConnectionManager(),
            NullLogger<NotificationService>.Instance,
            uow.Repository<UserNotification>(), uow.Repository<UserLogin>(), uow.Repository<User>(),
            uow.Repository<Trip>(), uow.Repository<Booking>(),
            uow.Repository<RideRequest>(), uow.Repository<RideRequestParticipant>(),
            new DriverAvailabilityService(uow.Repository<Trip>(), new FakeSecurityManager()));

    private static Trip TheTrip(int seatsLeft = 3)
    {
        var trip = Build.Trip(id: 10, driverId: DriverId, vehicleId: 1, seatsTotal: 4);
        trip.SeatsLeft = seatsLeft;
        trip.DepartAt = DateTime.UtcNow.AddMinutes(5);
        return trip;
    }

    /// <summary>
    /// An open posting on the same route, wanting one seat at about the hour the
    /// trip leaves — which is what the reverse match compares against.
    /// </summary>
    /// <summary>
    /// A trip waiting for a driver, on the same route, wanting one seat at about
    /// the hour the trip leaves — which is what the reverse match compares
    /// against.
    ///
    /// Seeded with its rider's participation, because that is where the riders
    /// to tell come from: a request with nobody on it is a pool nobody is
    /// waiting on.
    /// </summary>
    private static RideRequest Posting(FakeUnitOfWork uow, int id, int riderId, int seats = 1) =>
        Build.Demand(uow, id, riderId, seats, departAt: DateTime.UtcNow.AddMinutes(5));

    private static async Task<List<int>> Told(FakeUnitOfWork uow, Trip trip)
    {
        await Service(uow).NotifyWaitingRiders(trip, Build.Driver(DriverId));
        return uow.Store<UserNotification>()
            .Where(n => n.Type == NotificationType.TripMatched)
            .Select(n => n.UserId)
            .ToList();
    }

    [Fact]
    public async Task A_rider_waiting_on_the_same_route_is_told()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Rider(9));
        Posting(uow, 31, riderId: 9);

        Assert.Equal([9], await Told(uow, TheTrip()));
    }

    [Fact]
    public async Task A_rider_whose_request_already_closed_is_not_told()
    {
        var uow = new FakeUnitOfWork();
        var request = Posting(uow, 31, riderId: 9);
        request.Status = RideRequestStatus.Cancelled;

        Assert.Empty(await Told(uow, TheTrip()));
    }

    [Fact]
    public async Task A_rider_whose_posting_has_already_departed_is_not_told()
    {
        // Still Open on the row — the sweeper has not caught up — but past its
        // departure has come and gone, so it is over whatever the column says.
        var uow = new FakeUnitOfWork();
        var posting = Posting(uow, 31, riderId: 9);
        posting.DepartAt = DateTime.UtcNow.AddSeconds(-1);

        Assert.Empty(await Told(uow, TheTrip()));
    }

    [Fact]
    public async Task A_rider_asking_for_more_seats_than_are_left_is_not_told()
    {
        var uow = new FakeUnitOfWork();
        Posting(uow, 31, riderId: 9, seats: 3);

        Assert.Empty(await Told(uow, TheTrip(seatsLeft: 2)));
    }

    [Fact]
    public async Task The_driver_is_never_told_about_their_own_trip()
    {
        var uow = new FakeUnitOfWork();
        Posting(uow, 31, riderId: DriverId);

        Assert.Empty(await Told(uow, TheTrip()));
    }

    [Fact]
    public async Task Each_waiting_rider_is_told_once_however_many_postings_they_hold()
    {
        var uow = new FakeUnitOfWork();
        Posting(uow, 31, riderId: 9);
        Posting(uow, 32, riderId: 9);

        Assert.Equal([9], await Told(uow, TheTrip()));
    }
}
