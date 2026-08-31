using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Bookings.Models;
using Wanes.Areas.Services.Notifications;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Bookings;

public class BookingService : IBookingService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IRepository<Booking> bookingRepository;
    private readonly IRepository<Trip> tripRepository;

    public BookingService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IRepository<Booking> bookingRepository,
        IRepository<Trip> tripRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.bookingRepository = bookingRepository;
        this.tripRepository = tripRepository;
    }

    public async Task<BaseResponse<BookingOutput>> Create(CreateBookingInput input)
    {
        var riderId = securityManager.RequireUserId();
        var seats = input.Seats < 1 ? 1 : input.Seats;

        await unitOfWork.BeginTransactionAsync();
        try
        {
            var trip = tripRepository.FirstOrDefault(t => t.Id == input.TripId,
                query => query.Include(t => t.Driver));
            if (trip == null)
                return await RollBack<BookingOutput>(ErrorCode.TripNotFound);
            if (trip.DriverId == riderId)
                return await RollBack<BookingOutput>(ErrorCode.CannotBookOwnTrip);
            if (trip.Status != TripStatus.Posted)
                return await RollBack<BookingOutput>(ErrorCode.TripNotBookable);
            if (trip.SeatsLeft < seats)
                return await RollBack<BookingOutput>(ErrorCode.NoSeatsLeft);

            var alreadyBooked = await bookingRepository.AnyAsync(b =>
                b.TripId == trip.Id && b.RiderId == riderId && b.Status != BookingStatus.Cancelled);
            if (alreadyBooked)
                return await RollBack<BookingOutput>(ErrorCode.AlreadyBooked);

            // reserve seats atomically inside the transaction
            trip.SeatsLeft -= seats;
            if (trip.SeatsLeft <= 0) trip.Status = TripStatus.Full;
            tripRepository.Update(trip);

            var booking = new Booking
            {
                TripId = trip.Id,
                RiderId = riderId,
                Seats = seats,
                Status = BookingStatus.Confirmed,
            };
            bookingRepository.Create(booking);

            await unitOfWork.CommitAsync();
            await auditService.LogAsync(AuditActions.BookingConfirm, nameof(Booking), booking.Id);

            // Both sides care: the rider gets their receipt, the driver learns a
            // seat just went. Sent after the commit so a push can't outrun the row.
            await notificationService.Notify(riderId, NotificationTemplate.BookingConfirmedRider,
                args: new { origin = trip.OriginAddress, destination = trip.DestinationAddress },
                data:
                new { bookingId = booking.Id, tripId = trip.Id });

            await notificationService.Notify(trip.DriverId, NotificationTemplate.BookingConfirmedDriver,
                args: new { seats, seatsLeft = trip.SeatsLeft },
                data:
                new { bookingId = booking.Id, tripId = trip.Id });

            return new BaseResponse<BookingOutput>(new BookingOutput(booking, trip));
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }
    }

    public async Task<BaseResponse> Cancel(int id)
    {
        var riderId = securityManager.RequireUserId();

        await unitOfWork.BeginTransactionAsync();
        try
        {
            var booking = bookingRepository.FirstOrDefault(b => b.Id == id && b.RiderId == riderId);
            if (booking == null)
            {
                await unitOfWork.RollBackAsync();
                return new BaseResponse(ErrorCode.BookingNotFound);
            }
            if (booking.Status is BookingStatus.Cancelled or BookingStatus.Completed)
            {
                await unitOfWork.RollBackAsync();
                return new BaseResponse(ErrorCode.Conflict);
            }

            var trip = await tripRepository.GetByIdAsync(booking.TripId);

            booking.Status = BookingStatus.Cancelled;
            bookingRepository.Update(booking);

            if (trip != null)
            {
                // return the seats to the trip
                if (trip.Status is TripStatus.Posted or TripStatus.Full)
                    trip.SeatsLeft += booking.Seats;

                // The seat that just went may have been the one holding the trip
                // Full — or, mid-journey, the last one keeping it Active. Read the
                // trip back off its bookings rather than patching its status here,
                // so this and the driver's own moves cannot reach different answers.
                var seats = await bookingRepository
                    .Where(b => b.TripId == trip.Id)
                    .Select(b => new { b.Id, b.Status })
                    .ToListAsync();
                var statuses = seats
                    .Select(b => b.Id == booking.Id ? booking.Status : b.Status)
                    .ToList();
                trip.Status = TripStatusRules.Derive(trip.Status, statuses, trip.SeatsLeft);
                tripRepository.Update(trip);
            }

            await unitOfWork.CommitAsync();
            await auditService.LogAsync(AuditActions.BookingCancel, nameof(Booking), booking.Id);

            // The driver is the one who needs to know a seat came back.
            if (trip != null)
                await notificationService.Notify(trip.DriverId, NotificationTemplate.BookingCancelledDriver,
                    args: new
                    {
                        seats = booking.Seats,
                        origin = trip.OriginAddress,
                        destination = trip.DestinationAddress,
                    },
                    data:
                    new { bookingId = booking.Id, tripId = trip.Id });

            return new BaseResponse();
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }
    }

    public async Task<BaseResponse<List<BookingOutput>>> GetUserBookings()
    {
        var riderId = securityManager.RequireUserId();

        var bookings = await bookingRepository
            .Where(b => b.RiderId == riderId,
                query => query.Include(b => b.Trip).ThenInclude(t => t!.Driver))
            .OrderByDescending(b => b.Id)
            .ToListAsync();

        var data = bookings.Select(b => new BookingOutput(b, b.Trip)).ToList();
        return new BaseResponse<List<BookingOutput>>(data);
    }

    private async Task<BaseResponse<T>> RollBack<T>(ErrorCode errorCode)
    {
        await unitOfWork.RollBackAsync();
        return new BaseResponse<T>(default, errorCode);
    }
}
