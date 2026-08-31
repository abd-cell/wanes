using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Requests.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Requests;

public class RideRequestService : IRideRequestService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IRepository<RideRequest> rideRequestRepository;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<Booking> bookingRepository;

    public RideRequestService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IRepository<RideRequest> rideRequestRepository,
        IRepository<User> userRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<Trip> tripRepository,
        IRepository<Booking> bookingRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.rideRequestRepository = rideRequestRepository;
        this.userRepository = userRepository;
        this.vehicleRepository = vehicleRepository;
        this.tripRepository = tripRepository;
        this.bookingRepository = bookingRepository;
    }

    public async Task<BaseResponse<List<RideRequestRow>>> GetUserRequests()
    {
        var riderId = securityManager.RequireUserId();
        var requests = await rideRequestRepository
            .Where(r => r.RiderId == riderId)
            .OrderByDescending(r => r.Id).ToListAsync();

        var data = requests.Select(r => new RideRequestRow(r)).ToList();
        return new BaseResponse<List<RideRequestRow>>(data);
    }

    public async Task<BaseResponse> Cancel(int id)
    {
        var riderId = securityManager.RequireUserId();
        var request = rideRequestRepository.FirstOrDefault(r => r.Id == id && r.RiderId == riderId);
        if (request == null) return new BaseResponse(ErrorCode.RequestNotFound);
        if (request.Status != RideRequestStatus.Open) return new BaseResponse(ErrorCode.RequestNotOpen);

        request.Status = RideRequestStatus.Cancelled;
        rideRequestRepository.Update(request);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.RequestCancel, nameof(RideRequest), id);
        return new BaseResponse();
    }

    public async Task<BaseResponse<List<RideRequestRow>>> GetNearby(double lat, double lng, int radiusMeters)
    {
        var driverId = securityManager.RequireUserId();
        var origin = GeoFactory.Point(lat, lng);
        var radius = radiusMeters <= 0 ? 5000 : radiusMeters;

        // Two reaches, unioned: how far this driver is willing to look, and how
        // far the rider asked to be reached from. The second half is what makes
        // the list agree with the push — a rider who searched "Anywhere" gets a
        // 50 km hail, and the driver we already notified must be able to find it
        // here even though their own filter is narrower.
        var requests = await rideRequestRepository
            .Where(r => r.Status == RideRequestStatus.Open
                        && r.RiderId != driverId
                        && (r.ExpiresAt == null || r.ExpiresAt > DateTime.UtcNow)
                        && (r.Origin.IsWithinDistance(origin, radius)
                            || r.Origin.Distance(origin) <= r.RadiusMeters))
            .OrderBy(r => r.Origin.Distance(origin))
            .Take(30)
            .ToListAsync();

        var data = requests.Select(r => new RideRequestRow(r)).ToList();
        return new BaseResponse<List<RideRequestRow>>(data);
    }

    public async Task<BaseResponse<RideRequestRow>> Accept(int id)
    {
        var driverId = securityManager.RequireUserId();

        var driver = await userRepository.GetByIdAsync(driverId);
        if (driver == null) return new BaseResponse<RideRequestRow>(default, ErrorCode.NotFound);
        if (driver.DriverStatus != DriverStatus.Verified)
            return new BaseResponse<RideRequestRow>(default, ErrorCode.DriverNotVerified);

        // No admin sign-off on vehicles: owning one is enough to take a hail.
        var vehicle = vehicleRepository
            .Where(v => v.UserId == driverId)
            .OrderByDescending(v => v.IsDefault)
            .FirstOrDefault();
        if (vehicle == null) return new BaseResponse<RideRequestRow>(default, ErrorCode.VehicleNotFound);

        await unitOfWork.BeginTransactionAsync();
        try
        {
            var request = await rideRequestRepository.GetByIdAsync(id);
            if (request == null) return await RollBack(ErrorCode.RequestNotFound);
            if (request.Status != RideRequestStatus.Open) return await RollBack(ErrorCode.RequestNotOpen);
            if (request.Seats > vehicle.SeatCapacity) return await RollBack(ErrorCode.SeatsExceedCapacity);

            // a driver accepting a hail creates a trip for it + a confirmed booking
            var trip = new Trip
            {
                DriverId = driverId,
                VehicleId = vehicle.Id,
                OriginAddress = request.OriginAddress,
                Origin = request.Origin,
                DestinationAddress = request.DestinationAddress,
                Destination = request.Destination,
                Route = GeoFactory.Line(request.Origin, request.Destination),
                DepartAt = DateTime.UtcNow,
                SeatsTotal = vehicle.SeatCapacity,
                SeatsLeft = vehicle.SeatCapacity - request.Seats,
                Status = vehicle.SeatCapacity - request.Seats == 0 ? TripStatus.Full : TripStatus.Posted,
            };
            tripRepository.Create(trip);
            await unitOfWork.SaveAsync();

            bookingRepository.Create(new Booking
            {
                TripId = trip.Id,
                RiderId = request.RiderId,
                Seats = request.Seats,
                Status = BookingStatus.Confirmed,
                RideRequestId = request.Id,
            });

            request.Status = RideRequestStatus.Matched;
            request.MatchedTripId = trip.Id;
            rideRequestRepository.Update(request);

            await unitOfWork.CommitAsync();
            await auditService.LogAsync(AuditActions.RequestAccept, nameof(RideRequest), request.Id);

            await notificationService.Notify(request.RiderId, NotificationTemplate.DriverAcceptedRider,
                args: new { name = driver.FirstName },
                data:
                new { requestId = request.Id, tripId = trip.Id });

            return new BaseResponse<RideRequestRow>(new RideRequestRow(request));
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }
    }

    private async Task<BaseResponse<RideRequestRow>> RollBack(ErrorCode errorCode)
    {
        await unitOfWork.RollBackAsync();
        return new BaseResponse<RideRequestRow>(default, errorCode);
    }
}
