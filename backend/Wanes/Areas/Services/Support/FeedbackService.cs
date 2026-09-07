using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Support;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Support.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Support;

/// <summary>
/// The user's side of complaints and suggestions: file one, read the answers.
/// The desk's side lives in <see cref="Management.AdminFeedbackService"/>.
/// </summary>
public class FeedbackService : IFeedbackService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly IRepository<Feedback> feedbackRepository;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<Booking> bookingRepository;
    private readonly IRepository<User> userRepository;

    public FeedbackService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        IRepository<Feedback> feedbackRepository,
        IRepository<Trip> tripRepository,
        IRepository<Booking> bookingRepository,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.feedbackRepository = feedbackRepository;
        this.tripRepository = tripRepository;
        this.bookingRepository = bookingRepository;
        this.userRepository = userRepository;
    }

    public async Task<BaseResponse<FeedbackOutput>> Submit(FeedbackInput input)
    {
        var userId = securityManager.RequireUserId();

        var open = await feedbackRepository.CountAsync(f =>
            f.UserId == userId && (f.Status == FeedbackStatus.New || f.Status == FeedbackStatus.InReview));
        if (open >= FeedbackRules.MaxOpenPerUser)
            return new BaseResponse<FeedbackOutput>(default, ErrorCode.TooManyOpenFeedback);

        if (input.TripId != null && !await WasOnTrip(userId, input.TripId.Value))
            return new BaseResponse<FeedbackOutput>(default, ErrorCode.TripNotFound);

        // The language the account is set to, not the request's Accept-Language:
        // the clients send a fixed header, so the header would file every
        // Arabic complaint as English and the desk would answer in the wrong
        // language.
        var user = await userRepository.GetByIdAsync(userId);

        var feedback = new Feedback
        {
            Kind = input.Kind,
            UserId = userId,
            TripId = input.TripId,
            Subject = input.Subject.Trim(),
            Message = input.Message.Trim(),
            Language = user?.Language ?? Language.En,
            Status = FeedbackStatus.New,
        };

        feedbackRepository.Create(feedback);
        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.FeedbackSubmit, nameof(Feedback), feedback.Id);

        return new BaseResponse<FeedbackOutput>(new FeedbackOutput(feedback));
    }

    public async Task<BaseResponse<List<FeedbackOutput>>> Mine()
    {
        var userId = securityManager.RequireUserId();

        var items = await feedbackRepository.Query()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.Id)
            .ToListAsync();

        return new BaseResponse<List<FeedbackOutput>>(items.Select(f => new FeedbackOutput(f)).ToList());
    }

    /// <summary>
    /// Whether the user was actually on the trip they are complaining about —
    /// as its driver, or holding a booking on it.
    ///
    /// Checked because the trip id ends up in front of an admin as context: an
    /// arbitrary id would attach one user's complaint to a stranger's ride, and
    /// let anyone probe which trip ids exist.
    /// </summary>
    private async Task<bool> WasOnTrip(int userId, int tripId)
    {
        var trip = await tripRepository.GetByIdAsync(tripId);
        if (trip == null) return false;
        if (trip.DriverId == userId) return true;

        return await bookingRepository.AnyAsync(b => b.TripId == tripId && b.RiderId == userId);
    }
}
