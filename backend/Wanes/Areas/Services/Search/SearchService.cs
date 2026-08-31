using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Search.Models;
using Wanes.Areas.Services.Trips.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Search;

public class SearchService : ISearchService
{
    // MVP tuning — straight-line matching. Route-aware geometry comes later.
    // The radii and the time window live in MatchRules so the reverse match in
    // TripService and the hail push cannot drift away from what search used.
    private const int MaxResults = 20;

    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IRepository<RideRequest> rideRequestRepository;
    private readonly IRepository<Booking> bookingRepository;

    public SearchService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IRepository<Trip> tripRepository,
        IRepository<User> userRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<RideRequest> rideRequestRepository,
        IRepository<Booking> bookingRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.tripRepository = tripRepository;
        this.userRepository = userRepository;
        this.vehicleRepository = vehicleRepository;
        this.rideRequestRepository = rideRequestRepository;
        this.bookingRepository = bookingRepository;
    }

    public async Task<BaseResponse<SearchResult>> Search(SearchInput input)
    {
        var riderId = securityManager.RequireUserId();

        if (IsSamePoint(input.Origin, input.Destination))
            return new BaseResponse<SearchResult>(default, ErrorCode.OriginEqualsDestination);
        if (input.Seats < 1) input.Seats = 1;

        var origin = GeoFactory.Point(input.Origin.Lat, input.Origin.Lng);
        var destination = GeoFactory.Point(input.Destination.Lat, input.Destination.Lng);

        var now = DateTime.UtcNow;
        var from = input.When - MatchRules.TimeWindow;
        var to = input.When + MatchRules.TimeWindow;

        // A seat this rider already holds is not a seat they can take again --
        // booking it would only answer AlreadyBooked, so keep those trips out of
        // the results instead of showing an offer that cannot be accepted. A
        // rider's live bookings are few, so the ids come back in one round trip.
        var bookedTripIds = await bookingRepository
            .Where(b => b.RiderId == riderId && b.Status != BookingStatus.Cancelled)
            .Select(b => b.TripId)
            .ToListAsync();

        // The rider's Nearby toggle: keep both ends walkable, or open it up to
        // intercity distances at the cost of a longer walk.
        var matchRadius = MatchRules.RadiusFor(input.Nearby);

        // candidate filter (indexed geo + time); the rider's sort ranks it below
        var filtered = tripRepository
            .Where(t => t.Status == TripStatus.Posted
                        && t.SeatsLeft >= input.Seats
                        && t.DriverId != riderId
                        && !bookedTripIds.Contains(t.Id)
                        // A disabled account is not driving anyone anywhere.
                        && (t.Driver == null || !t.Driver.IsDisabled)
                        // The window reaches half an hour into the past, so a trip
                        // that already left -- and whose driver simply never pressed
                        // start -- would otherwise still be offered as bookable.
                        && t.DepartAt > now
                        && t.DepartAt >= from && t.DepartAt <= to
                        && t.Origin.IsWithinDistance(origin, matchRadius)
                        && t.Destination.IsWithinDistance(destination, matchRadius));

        var candidates = await Order(filtered, input.SortBy, origin, destination)
            .Take(MaxResults)
            .ToListAsync();

        if (candidates.Count > 0)
        {
            var driverIds = candidates.Select(t => t.DriverId).Distinct().ToList();
            var drivers = (await userRepository
                    .Where(u => driverIds.Contains(u.Id)).ToListAsync())
                .ToDictionary(u => u.Id);

            // The rider sees the car on the results/booking screens, so pull the
            // vehicles for the candidates in one round trip.
            var vehicleIds = candidates.Select(t => t.VehicleId).Distinct().ToList();
            var vehicles = (await vehicleRepository
                    .Where(v => vehicleIds.Contains(v.Id)).ToListAsync())
                .ToDictionary(v => v.Id);

            await auditService.LogAsync(AuditActions.SearchCarpool, nameof(Trip));

            var matches = candidates
                .Select(t => new TripOutput(
                    t,
                    drivers.GetValueOrDefault(t.DriverId),
                    vehicles.GetValueOrDefault(t.VehicleId)))
                .ToList();

            return new BaseResponse<SearchResult>(new SearchResult
            {
                Mode = SearchMode.Carpool,
                Matches = matches,
            });
        }

        // no match → open a ride request (HAIL)
        var request = new RideRequest
        {
            RiderId = riderId,
            OriginAddress = input.Origin.Address,
            Origin = origin,
            DestinationAddress = input.Destination.Address,
            Destination = destination,
            RequestedAt = DateTime.UtcNow,
            Seats = input.Seats,
            RadiusMeters = matchRadius,
            Status = RideRequestStatus.Open,
            ExpiresAt = DateTime.UtcNow.Add(MatchRules.HailTtl),
        };
        rideRequestRepository.Create(request);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.SearchHail, nameof(RideRequest), request.Id);

        // push to online drivers within the radius (FCM + SSE)
        var notified = await notificationService.NotifyNearbyDrivers(request);

        return new BaseResponse<SearchResult>(new SearchResult
        {
            Mode = SearchMode.Hail,
            RideRequestId = request.Id,
            DriversNotified = notified,
        });
    }

    /// <summary>
    /// The rider's chosen order, applied in the database so the cap above keeps
    /// the right twenty rather than the nearest twenty.
    ///
    /// Every sort ends on the combined-proximity ranking. Without that tie-break
    /// a page of same-priced or same-departure trips would come back in whatever
    /// order the query plan produced, and two identical searches could disagree.
    /// </summary>
    private static IOrderedQueryable<Trip> Order(IQueryable<Trip> query, SearchSort sort,
        Point origin, Point destination) => sort switch
    {
        SearchSort.Departure => query
            .OrderBy(t => t.DepartAt)
            .ThenBy(t => t.Origin.Distance(origin) + t.Destination.Distance(destination)),

        // Sorting on a nullable price puts the unpriced trips first in SQL, which
        // is the opposite of useful: a rider asking for "cheapest" wants a number.
        SearchSort.Price => query
            .OrderBy(t => t.PricePerSeat == null)
            .ThenBy(t => t.PricePerSeat)
            .ThenBy(t => t.Origin.Distance(origin) + t.Destination.Distance(destination)),

        SearchSort.Rating => query
            .OrderByDescending(t => t.Driver == null ? 0 : t.Driver.RatingAvg)
            .ThenBy(t => t.Origin.Distance(origin) + t.Destination.Distance(destination)),

        // Only the near end matters here — this is the rider's walk to the car.
        SearchSort.Pickup => query
            .OrderBy(t => t.Origin.Distance(origin))
            .ThenBy(t => t.Destination.Distance(destination)),

        SearchSort.Seats => query
            .OrderByDescending(t => t.SeatsLeft)
            .ThenBy(t => t.Origin.Distance(origin) + t.Destination.Distance(destination)),

        _ => query
            .OrderBy(t => t.Origin.Distance(origin) + t.Destination.Distance(destination)),
    };

    private static bool IsSamePoint(GeoPoint a, GeoPoint b) =>
        Math.Abs(a.Lat - b.Lat) < 1e-6 && Math.Abs(a.Lng - b.Lng) < 1e-6;
}
