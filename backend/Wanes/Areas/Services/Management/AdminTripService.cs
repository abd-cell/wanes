using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

public class AdminTripService : IAdminTripService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<Trip> tripRepository;

    public AdminTripService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<Trip> tripRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.tripRepository = tripRepository;
    }

    public async Task<BaseResponse<PageOutput<TripRow>>> List(PageInput page, TripStatus? status, int? driverId)
    {
        IQueryable<Trip> query = tripRepository.Query().Include(t => t.Driver).Include(t => t.Vehicle);

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(t =>
                t.OriginAddress.Contains(term) ||
                t.DestinationAddress.Contains(term));
        }
        if (status != null) query = query.Where(t => t.Status == status);
        if (driverId != null) query = query.Where(t => t.DriverId == driverId);

        var total = await query.CountAsync();
        var trips = await query.OrderByDescending(t => t.Id).Paginate(page).ToListAsync();

        var rows = trips.Select(BuildRow).ToList();

        return new BaseResponse<PageOutput<TripRow>>(new PageOutput<TripRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<TripRow>> Get(int id)
    {
        var trip = await tripRepository.Query()
            .Include(t => t.Driver)
            .Include(t => t.Vehicle)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (trip == null) return new BaseResponse<TripRow>(default, ErrorCode.NotFound);
        return new BaseResponse<TripRow>(BuildRow(trip));
    }

    public async Task<BaseResponse<TripRow>> Create(TripInput input)
    {
        var trip = new Trip();
        Apply(trip, input);
        tripRepository.Create(trip);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.trips.create", nameof(Trip), trip.Id);
        return new BaseResponse<TripRow>(await BuildRowWithDriver(trip.Id) ?? BuildRow(trip));
    }

    public async Task<BaseResponse<TripRow>> Update(int id, TripInput input)
    {
        var trip = await tripRepository.GetByIdAsync(id);
        if (trip == null) return new BaseResponse<TripRow>(default, ErrorCode.NotFound);

        Apply(trip, input);
        tripRepository.Update(trip);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.trips.update", nameof(Trip), trip.Id);
        return new BaseResponse<TripRow>(await BuildRowWithDriver(trip.Id) ?? BuildRow(trip));
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var trip = await tripRepository.GetByIdAsync(id);
        if (trip == null) return new BaseResponse(ErrorCode.NotFound);

        tripRepository.SoftDelete(trip);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.trips.delete", nameof(Trip), id);
        return new BaseResponse();
    }

    private static void Apply(Trip entity, TripInput input)
    {
        entity.DriverId = input.DriverId;
        entity.VehicleId = input.VehicleId;
        entity.OriginAddress = input.OriginAddress;
        entity.Origin = GeoFactory.Point(input.OriginLat, input.OriginLng);
        entity.DestinationAddress = input.DestinationAddress;
        entity.Destination = GeoFactory.Point(input.DestLat, input.DestLng);
        entity.Route = GeoFactory.Line(entity.Origin, entity.Destination);
        entity.DepartAt = input.DepartAt;
        entity.SeatsTotal = input.SeatsTotal;
        entity.SeatsLeft = input.SeatsLeft;
        entity.PricePerSeat = input.PricePerSeat;
        entity.Status = input.Status;

        // Posted and Full are a function of the seat count, not a free choice.
        // Editing seats without this can leave a sold-out trip sitting "Posted"
        // (or a trip with room stuck on "Full"). The lifecycle statuses the
        // driver drives -- Arrived/Active/Completed/Cancelled -- are left alone.
        if (entity.Status is TripStatus.Posted or TripStatus.Full)
            entity.Status = entity.SeatsLeft <= 0 ? TripStatus.Full : TripStatus.Posted;
    }

    private static TripRow BuildRow(Trip trip) => new TripRow(trip)
    {
        DriverName = AdminLabels.ForUser(trip.Driver),
        VehicleLabel = AdminLabels.ForVehicle(trip.Vehicle),
    };

    private async Task<TripRow?> BuildRowWithDriver(int id)
    {
        var trip = await tripRepository.Query()
            .Include(t => t.Driver)
            .Include(t => t.Vehicle)
            .FirstOrDefaultAsync(t => t.Id == id);
        return trip == null ? null : BuildRow(trip);
    }
}
