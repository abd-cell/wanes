using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Marketplace.Models;
using Wanes.Areas.Services.Notifications;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Marketplace;

[TransientInjectable]
public interface IAdminRideRequestService
{
    Task<BaseResponse<PageOutput<RideRequestAdminRow>>> List(PageInput page, RideRequestStatus? status);
    Task<BaseResponse<RideRequestAdminRow>> Get(int id);
    Task<BaseResponse> Cancel(int id);
}

/// <summary>
/// Demand, as the admin console sees it — the one marketplace object the
/// console had no screen for.
/// </summary>
public class AdminRideRequestService : IAdminRideRequestService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly IRepository<RideRequest> requestRepository;
    private readonly IRepository<RideRequestParticipant> participantRepository;
    private readonly IRepository<DriverInterest> interestRepository;

    public AdminRideRequestService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        INotificationService notificationService,
        IRepository<RideRequest> requestRepository,
        IRepository<RideRequestParticipant> participantRepository,
        IRepository<DriverInterest> interestRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.requestRepository = requestRepository;
        this.participantRepository = participantRepository;
        this.interestRepository = interestRepository;
    }

    public async Task<BaseResponse<PageOutput<RideRequestAdminRow>>> List(PageInput page, RideRequestStatus? status)
    {
        var query = requestRepository.Query();
        if (status != null) query = query.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(r => r.OriginAddress.Contains(term) || r.DestinationAddress.Contains(term));
        }

        var total = await query.CountAsync();
        var requests = await query.OrderByDescending(r => r.Id).Paginate(page).ToListAsync();
        return new BaseResponse<PageOutput<RideRequestAdminRow>>(new PageOutput<RideRequestAdminRow>
        {
            TotalRows = total,
            Data = await Rows(requests),
        });
    }

    public async Task<BaseResponse<RideRequestAdminRow>> Get(int id)
    {
        var request = requestRepository.FirstOrDefault(r => r.Id == id);
        if (request == null) return new BaseResponse<RideRequestAdminRow>(default, ErrorCode.RideRequestNotFound);
        return new BaseResponse<RideRequestAdminRow>((await Rows([request])).First());
    }

    public async Task<BaseResponse> Cancel(int id)
    {
        var request = requestRepository.FirstOrDefault(r => r.Id == id);
        if (request == null) return new BaseResponse(ErrorCode.RideRequestNotFound);
        if (request.Status != RideRequestStatus.Open) return new BaseResponse(ErrorCode.RideRequestNotOpen);

        request.Status = RideRequestStatus.Cancelled;
        requestRepository.Update(request);

        var live = await interestRepository
            .Where(i => i.RideRequestId == id && i.Status == DriverInterestStatus.Interested)
            .ToListAsync();
        foreach (var interest in live)
        {
            interest.Status = DriverInterestStatus.Expired;
            interestRepository.Update(interest);
        }
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.AdminRideRequestCancel, nameof(RideRequest), id);
        await notificationService.NotifyRideRequestClosed(id, RiderTripClosedReason.Cancelled);
        return new BaseResponse();
    }

    private async Task<List<RideRequestAdminRow>> Rows(IReadOnlyCollection<RideRequest> requests)
    {
        var ids = requests.Select(r => r.Id).ToList();
        var participants = await participantRepository
            .Where(p => ids.Contains(p.RideRequestId) && p.Status == RideRequestParticipantStatus.Active,
                q => q.Include(p => p.Rider))
            .ToListAsync();
        var offers = await interestRepository
            .Where(i => ids.Contains(i.RideRequestId) && i.Status == DriverInterestStatus.Interested)
            .Select(i => i.RideRequestId)
            .ToListAsync();

        return requests.Select(r =>
        {
            var mine = participants.Where(p => p.RideRequestId == r.Id).OrderBy(p => p.Id).ToList();
            var author = mine.FirstOrDefault()?.Rider;
            return new RideRequestAdminRow
            {
                Id = r.Id,
                OriginAddress = r.OriginAddress,
                DestinationAddress = r.DestinationAddress,
                DepartAt = r.DepartAt,
                SeatsRequested = r.SeatsRequested,
                RiderCount = mine.Count,
                AuthorName = author == null ? null : $"{author.FirstName} {author.LastName}".Trim(),
                Status = r.Status,
                DriverGenderPolicy = r.DriverGenderPolicy,
                CoRiderGenderPolicy = r.GenderPolicy,
                InterestCount = offers.Count(o => o == r.Id),
                FirstInterestAt = r.FirstInterestAt,
                DecideAt = r.DecideAt,
                NotifiedAt = r.NotifiedAt,
                MatchedTripId = r.MatchedTripId,
                ReopenedFromRequestId = r.ReopenedFromRequestId,
                ScheduleId = r.ScheduleId,
                CreatedAt = r.CreationDate,
            };
        }).ToList();
    }
}
