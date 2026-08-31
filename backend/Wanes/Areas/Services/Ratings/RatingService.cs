using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Ratings;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Ratings.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Ratings;

public class RatingService : IRatingService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IRepository<Booking> bookingRepository;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<Rating> ratingRepository;
    private readonly IRepository<User> userRepository;

    public RatingService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        IRepository<Booking> bookingRepository,
        IRepository<Trip> tripRepository,
        IRepository<Rating> ratingRepository,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.bookingRepository = bookingRepository;
        this.tripRepository = tripRepository;
        this.ratingRepository = ratingRepository;
        this.userRepository = userRepository;
    }

    public async Task<BaseResponse> Rate(CreateRatingInput input)
    {
        var userId = securityManager.RequireUserId();
        if (input.Stars is < 1 or > 5)
            return new BaseResponse(ErrorCode.ValidationError, "Stars must be 1..5.");

        var booking = await bookingRepository.GetByIdAsync(input.BookingId);
        if (booking == null) return new BaseResponse(ErrorCode.BookingNotFound);
        if (booking.Status != BookingStatus.Completed)
            return new BaseResponse(ErrorCode.RatingNotAllowed);

        var trip = await tripRepository.GetByIdAsync(booking.TripId);
        if (trip == null) return new BaseResponse(ErrorCode.TripNotFound);

        var isRider = booking.RiderId == userId;
        var isDriver = trip.DriverId == userId;
        if (!isRider && !isDriver) return new BaseResponse(ErrorCode.Forbidden);

        var direction = isRider ? RatingDirection.RiderToDriver : RatingDirection.DriverToRider;
        var toUserId = isRider ? trip.DriverId : booking.RiderId;

        var alreadyRated = await ratingRepository.AnyAsync(r =>
            r.BookingId == booking.Id && r.Direction == direction);
        if (alreadyRated) return new BaseResponse(ErrorCode.AlreadyRated);

        await unitOfWork.BeginTransactionAsync();
        try
        {
            ratingRepository.Create(new Rating
            {
                BookingId = booking.Id,
                FromUserId = userId,
                ToUserId = toUserId,
                Direction = direction,
                Stars = input.Stars,
                Comment = input.Comment,
            });

            // recompute the target user's running average
            var target = await userRepository.GetByIdAsync(toUserId);
            if (target != null)
            {
                var newCount = target.RatingCount + 1;
                target.RatingAvg = ((target.RatingAvg * target.RatingCount) + input.Stars) / newCount;
                target.RatingCount = newCount;
                userRepository.Update(target);
            }

            await unitOfWork.CommitAsync();
        }
        catch
        {
            await unitOfWork.RollBackAsync();
            throw;
        }

        await auditService.LogAsync(AuditActions.RatingCreate, nameof(Rating), booking.Id);

        // Fired after the commit: a delivery failure must not roll back a rating
        // that is already counted in the target's running average.
        await notificationService.Notify(toUserId, NotificationTemplate.RatingReceived,
            args: new { stars = input.Stars },
            data: new { bookingId = booking.Id, tripId = trip.Id, stars = input.Stars });

        return new BaseResponse();
    }
}
