using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Trips.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Trips;

public class TripService : ITripService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<TripStatusHistory> tripHistoryRepository;
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<Booking> bookingRepository;

    public TripService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        IRepository<Trip> tripRepository,
        IRepository<TripStatusHistory> tripHistoryRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<User> userRepository,
        IRepository<Booking> bookingRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.tripRepository = tripRepository;
        this.tripHistoryRepository = tripHistoryRepository;
        this.vehicleRepository = vehicleRepository;
        this.userRepository = userRepository;
        this.bookingRepository = bookingRepository;
    }

    public async Task<BaseResponse<TripOutput>> Create(CreateTripInput input)
    {
        var driverId = securityManager.RequireUserId();

        var driver = await userRepository.GetByIdAsync(driverId);
        if (driver == null) return new BaseResponse<TripOutput>(default, ErrorCode.NotFound);

        // No admin sign-off on trips: owning the vehicle is enough, and the trip
        // goes out as Posted the moment the driver creates it.
        var vehicle = vehicleRepository.FirstOrDefault(v => v.Id == input.VehicleId && v.UserId == driverId);
        if (vehicle == null) return new BaseResponse<TripOutput>(default, ErrorCode.VehicleNotFound);

        if (input.DepartAt <= DateTime.UtcNow)
            return new BaseResponse<TripOutput>(default, ErrorCode.DepartureMustBeFuture);
        if (IsSamePoint(input.Origin, input.Destination))
            return new BaseResponse<TripOutput>(default, ErrorCode.OriginEqualsDestination);

        // seats offered defaults to capacity, and must never exceed it
        var seats = input.SeatsTotal <= 0 ? vehicle.SeatCapacity : input.SeatsTotal;
        if (seats > vehicle.SeatCapacity)
            return new BaseResponse<TripOutput>(default, ErrorCode.SeatsExceedCapacity);

        var origin = GeoFactory.Point(input.Origin.Lat, input.Origin.Lng);
        var destination = GeoFactory.Point(input.Destination.Lat, input.Destination.Lng);

        var trip = new Trip
        {
            DriverId = driverId,
            VehicleId = vehicle.Id,
            OriginAddress = input.Origin.Address,
            Origin = origin,
            DestinationAddress = input.Destination.Address,
            Destination = destination,
            Route = GeoFactory.Line(origin, destination),   // straight line in the MVP
            DepartAt = input.DepartAt,
            SeatsTotal = seats,
            SeatsLeft = seats,
            PricePerSeat = input.PricePerSeat,
            Status = TripStatus.Posted,
        };

        tripRepository.Create(trip);
        await unitOfWork.SaveAsync();
        AddHistory(trip.Id, TripStatus.Posted, driverId);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.TripCreate, nameof(Trip), trip.Id);

        return new BaseResponse<TripOutput>(new TripOutput(trip, driver, vehicle));
    }

    public async Task<BaseResponse<TripOutput>> Update(int id, UpdateTripInput input)
    {
        var driverId = securityManager.RequireUserId();

        var trip = tripRepository.FirstOrDefault(t => t.Id == id && t.DriverId == driverId,
            query => query.Include(t => t.Driver).Include(t => t.Vehicle));
        if (trip == null) return new BaseResponse<TripOutput>(default, ErrorCode.TripNotFound);

        // Only a still-posted trip nobody has taken a seat on can be edited; once
        // it carries a booking the driver has to cancel instead.
        if (trip.Status != TripStatus.Posted)
            return new BaseResponse<TripOutput>(default, ErrorCode.TripNotEditable);

        var hasBookings = await bookingRepository
            .Where(b => b.TripId == trip.Id && b.Status != BookingStatus.Cancelled)
            .AnyAsync();
        if (hasBookings) return new BaseResponse<TripOutput>(default, ErrorCode.TripNotEditable);

        var vehicle = vehicleRepository.FirstOrDefault(v => v.Id == input.VehicleId && v.UserId == driverId);
        if (vehicle == null) return new BaseResponse<TripOutput>(default, ErrorCode.VehicleNotFound);

        if (input.DepartAt <= DateTime.UtcNow)
            return new BaseResponse<TripOutput>(default, ErrorCode.DepartureMustBeFuture);
        if (IsSamePoint(input.Origin, input.Destination))
            return new BaseResponse<TripOutput>(default, ErrorCode.OriginEqualsDestination);

        var seats = input.SeatsTotal <= 0 ? vehicle.SeatCapacity : input.SeatsTotal;
        if (seats > vehicle.SeatCapacity)
            return new BaseResponse<TripOutput>(default, ErrorCode.SeatsExceedCapacity);

        var origin = GeoFactory.Point(input.Origin.Lat, input.Origin.Lng);
        var destination = GeoFactory.Point(input.Destination.Lat, input.Destination.Lng);

        trip.VehicleId = vehicle.Id;
        trip.OriginAddress = input.Origin.Address;
        trip.Origin = origin;
        trip.DestinationAddress = input.Destination.Address;
        trip.Destination = destination;
        trip.Route = GeoFactory.Line(origin, destination);
        trip.DepartAt = input.DepartAt;
        trip.SeatsTotal = seats;
        trip.SeatsLeft = seats;              // no bookings yet, so every seat is free
        trip.PricePerSeat = input.PricePerSeat;

        tripRepository.Update(trip);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.TripUpdate, nameof(Trip), trip.Id);

        return new BaseResponse<TripOutput>(new TripOutput(trip, trip.Driver, vehicle));
    }

    public Task<BaseResponse<TripOutput>> Get(int id)
    {
        var trip = tripRepository.FirstOrDefault(t => t.Id == id, query => query.Include(t => t.Driver).Include(t => t.Vehicle));
        if (trip == null) return Task.FromResult(new BaseResponse<TripOutput>(default, ErrorCode.TripNotFound));
        return Task.FromResult(new BaseResponse<TripOutput>(new TripOutput(trip, trip.Driver)));
    }

    public async Task<BaseResponse<List<TripOutput>>> GetUserTrips()
    {
        var driverId = securityManager.RequireUserId();
        var trips = await tripRepository
            .Where(t => t.DriverId == driverId, query => query.Include(t => t.Driver).Include(t => t.Vehicle))
            .OrderByDescending(t => t.DepartAt)
            .ToListAsync();

        var data = trips.Select(t => new TripOutput(t, t.Driver)).ToList();
        return new BaseResponse<List<TripOutput>>(data);
    }

    public async Task<BaseResponse> Cancel(int id)
    {
        var driverId = securityManager.RequireUserId();
        var trip = tripRepository.FirstOrDefault(t => t.Id == id && t.DriverId == driverId);
        if (trip == null) return new BaseResponse(ErrorCode.TripNotFound);
        if (trip.Status is TripStatus.Completed or TripStatus.Cancelled)
            return new BaseResponse(ErrorCode.TripNotBookable);

        await unitOfWork.BeginTransactionAsync();
        try
        {
            trip.Status = TripStatus.Cancelled;
            tripRepository.Update(trip);

            // auto-cancel all confirmed/pending bookings
            var bookings = await bookingRepository
                .Where(b => b.TripId == trip.Id &&
                    (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed))
                .ToListAsync();
            foreach (var booking in bookings)
            {
                booking.Status = BookingStatus.Cancelled;
                bookingRepository.Update(booking);
            }

            AddHistory(trip.Id, TripStatus.Cancelled, driverId);
            await unitOfWork.CommitAsync();
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }

        await auditService.LogAsync(AuditActions.TripCancel, nameof(Trip), trip.Id);
        return new BaseResponse();
    }

    public Task<BaseResponse<TripOutput>> Start(int id) => Transition(id, TripStatus.Active, AuditActions.TripStart);
    public Task<BaseResponse<TripOutput>> Complete(int id) => Transition(id, TripStatus.Completed, AuditActions.TripComplete);

    private async Task<BaseResponse<TripOutput>> Transition(int id, TripStatus to, string action)
    {
        var driverId = securityManager.RequireUserId();
        var trip = tripRepository.FirstOrDefault(t => t.Id == id && t.DriverId == driverId,
            query => query.Include(t => t.Driver).Include(t => t.Vehicle));
        if (trip == null) return new BaseResponse<TripOutput>(default, ErrorCode.TripNotFound);

        trip.Status = to;
        tripRepository.Update(trip);

        if (to == TripStatus.Completed)
        {
            // complete in-progress bookings + bump driver trip count
            var bookings = await bookingRepository
                .Where(b => b.TripId == trip.Id && b.Status != BookingStatus.Cancelled)
                .ToListAsync();
            foreach (var booking in bookings)
            {
                booking.Status = BookingStatus.Completed;
                bookingRepository.Update(booking);
            }
            if (trip.Driver != null)
            {
                trip.Driver.TripsAsDriver++;
                userRepository.Update(trip.Driver);
            }
        }

        AddHistory(trip.Id, to, driverId);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(action, nameof(Trip), trip.Id);
        return new BaseResponse<TripOutput>(new TripOutput(trip, trip.Driver));
    }

    private void AddHistory(int tripId, TripStatus status, int changedBy) =>
        tripHistoryRepository.Create(new TripStatusHistory
        {
            TripId = tripId,
            Status = status,
            ChangedBy = changedBy,
        });

    private static bool IsSamePoint(GeoPoint a, GeoPoint b) =>
        Math.Abs(a.Lat - b.Lat) < 1e-6 && Math.Abs(a.Lng - b.Lng) < 1e-6;
}
