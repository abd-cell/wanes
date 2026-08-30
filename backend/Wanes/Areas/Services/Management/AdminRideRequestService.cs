using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

public class AdminRideRequestService : IAdminRideRequestService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<RideRequest> rideRequestRepository;
    private readonly IRepository<Trip> tripRepository;

    public AdminRideRequestService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<RideRequest> rideRequestRepository,
        IRepository<Trip> tripRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.rideRequestRepository = rideRequestRepository;
        this.tripRepository = tripRepository;
    }

    public async Task<BaseResponse<PageOutput<RideRequestRow>>> List(PageInput page, RideRequestStatus? status, int? riderId)
    {
        IQueryable<RideRequest> query = rideRequestRepository.Query().Include(r => r.Rider);

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(r =>
                r.OriginAddress.Contains(term) ||
                r.DestinationAddress.Contains(term));
        }
        if (status != null) query = query.Where(r => r.Status == status);
        if (riderId != null) query = query.Where(r => r.RiderId == riderId);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(r => r.Id).Paginate(page).ToListAsync();

        var rows = await BuildRows(items);

        return new BaseResponse<PageOutput<RideRequestRow>>(new PageOutput<RideRequestRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<RideRequestRow>> Get(int id)
    {
        var request = await rideRequestRepository.Query().Include(r => r.Rider).FirstOrDefaultAsync(r => r.Id == id);
        if (request == null) return new BaseResponse<RideRequestRow>(default, ErrorCode.NotFound);
        return new BaseResponse<RideRequestRow>((await BuildRows([request])).First());
    }

    public async Task<BaseResponse<RideRequestRow>> Create(RideRequestInput input)
    {
        var request = new RideRequest();
        Apply(request, input);
        rideRequestRepository.Create(request);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.requests.create", nameof(RideRequest), request.Id);
        return await Get(request.Id);
    }

    public async Task<BaseResponse<RideRequestRow>> Update(int id, RideRequestInput input)
    {
        var request = await rideRequestRepository.GetByIdAsync(id);
        if (request == null) return new BaseResponse<RideRequestRow>(default, ErrorCode.NotFound);

        Apply(request, input);
        rideRequestRepository.Update(request);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.requests.update", nameof(RideRequest), request.Id);
        return await Get(request.Id);
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var request = await rideRequestRepository.GetByIdAsync(id);
        if (request == null) return new BaseResponse(ErrorCode.NotFound);

        rideRequestRepository.SoftDelete(request);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.requests.delete", nameof(RideRequest), id);
        return new BaseResponse();
    }

    private static void Apply(RideRequest request, RideRequestInput input)
    {
        request.RiderId = input.RiderId;
        request.OriginAddress = input.OriginAddress;
        request.Origin = GeoFactory.Point(input.OriginLat, input.OriginLng);
        request.DestinationAddress = input.DestinationAddress;
        request.Destination = GeoFactory.Point(input.DestLat, input.DestLng);
        request.Seats = input.Seats;
        request.RadiusMeters = input.RadiusMeters;
        request.Status = input.Status;
        request.MatchedTripId = input.MatchedTripId;
        request.ExpiresAt = input.ExpiresAt;
    }

    /// <summary>
    /// Adds the rider name and the matched trip route. MatchedTripId has no navigation
    /// property, so the trips for the whole page are resolved in one extra query.
    /// </summary>
    private async Task<List<RideRequestRow>> BuildRows(IReadOnlyCollection<RideRequest> requests)
    {
        var tripIds = requests.Where(r => r.MatchedTripId != null)
            .Select(r => r.MatchedTripId!.Value).Distinct().ToList();

        var trips = tripIds.Count == 0
            ? new Dictionary<int, Trip>()
            : await tripRepository.Where(t => tripIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id);

        return requests.Select(r => new RideRequestRow(r)
        {
            RiderName = AdminLabels.ForUser(r.Rider),
            MatchedTripSummary = r.MatchedTripId != null && trips.TryGetValue(r.MatchedTripId.Value, out var trip)
                ? AdminLabels.ForTrip(trip)
                : null,
        }).ToList();
    }
}
