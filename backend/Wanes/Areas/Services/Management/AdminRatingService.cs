using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Ratings;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

public class AdminRatingService : IAdminRatingService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<Rating> ratingRepository;

    public AdminRatingService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<Rating> ratingRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.ratingRepository = ratingRepository;
    }

    public async Task<BaseResponse<PageOutput<RatingRow>>> List(PageInput page, RatingDirection? direction, int? toUserId, int? fromUserId)
    {
        IQueryable<Rating> query = ratingRepository.Query()
            .Include(r => r.FromUser)
            .Include(r => r.ToUser)
            .Include(r => r.Booking).ThenInclude(b => b!.Rider)
            .Include(r => r.Booking).ThenInclude(b => b!.Trip);

        if (direction != null) query = query.Where(r => r.Direction == direction);
        if (toUserId != null) query = query.Where(r => r.ToUserId == toUserId);
        if (fromUserId != null) query = query.Where(r => r.FromUserId == fromUserId);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(r => r.Id).Paginate(page).ToListAsync();

        var rows = items.Select(BuildRow).ToList();

        return new BaseResponse<PageOutput<RatingRow>>(new PageOutput<RatingRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<RatingRow>> Get(int id)
    {
        var rating = await ratingRepository.Query()
            .Include(r => r.FromUser)
            .Include(r => r.ToUser)
            .Include(r => r.Booking).ThenInclude(b => b!.Rider)
            .Include(r => r.Booking).ThenInclude(b => b!.Trip)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (rating == null) return new BaseResponse<RatingRow>(default, ErrorCode.NotFound);
        return new BaseResponse<RatingRow>(BuildRow(rating));
    }

    public async Task<BaseResponse<RatingRow>> Create(RatingInput input)
    {
        var rating = new Rating();
        Apply(rating, input);
        ratingRepository.Create(rating);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.ratings.create", nameof(Rating), rating.Id);
        return await Get(rating.Id);
    }

    public async Task<BaseResponse<RatingRow>> Update(int id, RatingInput input)
    {
        var rating = await ratingRepository.GetByIdAsync(id);
        if (rating == null) return new BaseResponse<RatingRow>(default, ErrorCode.NotFound);

        Apply(rating, input);
        ratingRepository.Update(rating);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.ratings.update", nameof(Rating), rating.Id);
        return await Get(rating.Id);
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var rating = await ratingRepository.GetByIdAsync(id);
        if (rating == null) return new BaseResponse(ErrorCode.NotFound);

        ratingRepository.SoftDelete(rating);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.ratings.delete", nameof(Rating), id);
        return new BaseResponse();
    }

    private static void Apply(Rating rating, RatingInput input)
    {
        rating.BookingId = input.BookingId;
        rating.FromUserId = input.FromUserId;
        rating.ToUserId = input.ToUserId;
        rating.Direction = input.Direction;
        rating.Stars = input.Stars;
        rating.Comment = input.Comment;
    }

    private static RatingRow BuildRow(Rating rating) => new(rating)
    {
        FromName = AdminLabels.ForUser(rating.FromUser),
        ToName = AdminLabels.ForUser(rating.ToUser),
        BookingSummary = AdminLabels.ForBooking(rating.Booking),
    };
}
