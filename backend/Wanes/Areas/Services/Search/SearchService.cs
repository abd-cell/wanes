using Microsoft.EntityFrameworkCore;
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
    private const double MatchRadiusMeters = 3000;
    private static readonly TimeSpan TimeWindow = TimeSpan.FromMinutes(30);
    private const int MaxResults = 20;
    private const int HailRadiusMeters = 2000;
    private static readonly TimeSpan HailTtl = TimeSpan.FromMinutes(10);

    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IRepository<RideRequest> rideRequestRepository;

    public SearchService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IRepository<Trip> tripRepository,
        IRepository<User> userRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<RideRequest> rideRequestRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.tripRepository = tripRepository;
        this.userRepository = userRepository;
        this.vehicleRepository = vehicleRepository;
        this.rideRequestRepository = rideRequestRepository;
    }

    public async Task<BaseResponse<SearchResult>> Search(SearchInput input)
    {
        var riderId = securityManager.RequireUserId();

        if (IsSamePoint(input.Origin, input.Destination))
            return new BaseResponse<SearchResult>(default, ErrorCode.OriginEqualsDestination);
        if (input.Seats < 1) input.Seats = 1;

        var origin = GeoFactory.Point(input.Origin.Lat, input.Origin.Lng);
        var destination = GeoFactory.Point(input.Destination.Lat, input.Destination.Lng);

        var from = input.When - TimeWindow;
        var to = input.When + TimeWindow;

        // candidate filter (indexed geo + time), ranked by combined distance
        var candidates = await tripRepository
            .Where(t => t.Status == TripStatus.Posted
                        && t.SeatsLeft >= input.Seats
                        && t.DriverId != riderId
                        && t.DepartAt >= from && t.DepartAt <= to
                        && t.Origin.IsWithinDistance(origin, MatchRadiusMeters)
                        && t.Destination.IsWithinDistance(destination, MatchRadiusMeters))
            .OrderBy(t => t.Origin.Distance(origin) + t.Destination.Distance(destination))
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
            RadiusMeters = HailRadiusMeters,
            Status = RideRequestStatus.Open,
            ExpiresAt = DateTime.UtcNow.Add(HailTtl),
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

    private static bool IsSamePoint(GeoPoint a, GeoPoint b) =>
        Math.Abs(a.Lat - b.Lat) < 1e-6 && Math.Abs(a.Lng - b.Lng) < 1e-6;
}
