using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.Schedules;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Bookings;
using Wanes.Areas.Services.RideRequests;
using Wanes.Areas.Services.Schedules;
using Wanes.Areas.Services.Search;
using Wanes.Areas.Services.Trips;
using Wanes.Areas.Services.Users.Availability;

namespace Wanes.Tests.TestDoubles;

/// <summary>
/// Wires the real services over the in-memory doubles.
///
/// One place, because the graphs are the interesting part of the arrangement
/// and they are not what any test is about: a booking needs a confirmation
/// service, which needs a configuration service, which nine tests do not care
/// about. When each file built its own, adding a dependency meant editing
/// twenty constructors and every one of those edits was a chance to wire a
/// different fake in.
///
/// The services themselves are real. Only the boundaries are doubled —
/// repositories, the clock's callers, audit, notifications, settings — which is
/// the whole point of the suite: it tests the rules, not the mocks.
/// </summary>
public static class Make
{
    /// <summary>
    /// The rider-side twin of <see cref="DriverAvailabilityService"/>, wired the
    /// same way: a real service over the in-memory repositories.
    /// </summary>
    public static RiderAvailabilityService RiderAvailability(FakeUnitOfWork uow, int riderId) =>
        new(uow.Repository<Booking>(), uow.Repository<Trip>(),
            new FakeSecurityManager(riderId));

    public static TripService Trips(FakeUnitOfWork uow, int driverId,
        FakeNotificationService? notifications = null, FakeAppConfigurationService? config = null) =>
        new(uow, new FakeSecurityManager(driverId), new FakeAuditService(),
            notifications ?? new FakeNotificationService(),
            new DriverAvailabilityService(uow.Repository<Trip>(), new FakeSecurityManager(driverId)),
            config ?? new FakeAppConfigurationService(),
            uow.Repository<Trip>(), uow.Repository<TripStatusHistory>(), uow.Repository<Vehicle>(),
            uow.Repository<User>(), uow.Repository<Booking>());

    public static TripConfirmationService Confirmations(FakeUnitOfWork uow, int driverId,
        FakeNotificationService? notifications = null, FakeAppConfigurationService? config = null) =>
        new(uow, new FakeSecurityManager(driverId), new FakeAuditService(),
            notifications ?? new FakeNotificationService(),
            config ?? new FakeAppConfigurationService(),
            uow.Repository<Trip>(), uow.Repository<TripStatusHistory>(), uow.Repository<Booking>());

    public static BookingService Bookings(FakeUnitOfWork uow, int riderId,
        FakeNotificationService? notifications = null, FakeAppConfigurationService? config = null)
    {
        notifications ??= new FakeNotificationService();
        return new BookingService(uow, new FakeSecurityManager(riderId), new FakeAuditService(),
            notifications, Confirmations(uow, riderId, notifications, config),
            RiderAvailability(uow, riderId),
            uow.Repository<Booking>(), uow.Repository<Trip>(), uow.Repository<User>());
    }

    /// <summary>The riders' side of demand: create, join, leave, board, sweeps.</summary>
    public static RideRequestService Requests(FakeUnitOfWork uow, int userId,
        FakeNotificationService? notifications = null, FakeAppConfigurationService? config = null) =>
        new(uow, new FakeSecurityManager(userId), new FakeAuditService(),
            notifications ?? new FakeNotificationService(),
            new DriverAvailabilityService(uow.Repository<Trip>(), new FakeSecurityManager(userId)),
            RiderAvailability(uow, userId),
            config ?? new FakeAppConfigurationService(),
            uow.Repository<User>(), uow.Repository<RideRequest>(),
            uow.Repository<RideRequestParticipant>(), uow.Repository<DriverInterest>());

    /// <summary>
    /// The drivers' side: offer, withdraw, and the selection that forms a trip.
    ///
    /// The config double decides which marketplace this is — leave it alone for
    /// first-come-first-served (the shipped setting), or set
    /// <c>DriverSelectionWindowMinutes</c> to test competing offers.
    /// </summary>
    public static DriverInterestService Interests(FakeUnitOfWork uow, int userId,
        FakeNotificationService? notifications = null, FakeAppConfigurationService? config = null) =>
        new(uow, new FakeSecurityManager(userId), new FakeAuditService(),
            notifications ?? new FakeNotificationService(),
            new DriverAvailabilityService(uow.Repository<Trip>(), new FakeSecurityManager(userId)),
            config ?? new FakeAppConfigurationService(),
            uow.Repository<User>(), uow.Repository<Vehicle>(),
            uow.Repository<Trip>(), uow.Repository<TripStatusHistory>(),
            uow.Repository<Booking>(), uow.Repository<RideRequest>(),
            uow.Repository<RideRequestParticipant>(), uow.Repository<DriverInterest>());

    public static SearchService Search(FakeUnitOfWork uow, int riderId,
        FakeAppConfigurationService? config = null) =>
        new(new FakeSecurityManager(riderId), new FakeAuditService(),
            config ?? new FakeAppConfigurationService(),
            uow.Repository<Trip>(), uow.Repository<User>(), uow.Repository<Booking>(),
            uow.Repository<RideRequest>(), uow.Repository<RideRequestParticipant>());

    public static TripScheduleService Schedules(FakeUnitOfWork uow, int ownerId,
        FakeAppConfigurationService? config = null) =>
        new(uow, new FakeSecurityManager(ownerId), new FakeAuditService(),
            config ?? new FakeAppConfigurationService(),
            uow.Repository<TripSchedule>(), uow.Repository<Trip>(),
            uow.Repository<RideRequest>(), uow.Repository<RideRequestParticipant>(),
            uow.Repository<Booking>(), uow.Repository<Vehicle>(), uow.Repository<User>());
}
