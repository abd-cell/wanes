using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

public class AdminVehicleService : IAdminVehicleService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<Vehicle> vehicleRepository;

    public AdminVehicleService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<Vehicle> vehicleRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.vehicleRepository = vehicleRepository;
    }

    public async Task<BaseResponse<PageOutput<VehicleRow>>> List(PageInput page, int? userId)
    {
        IQueryable<Vehicle> query = vehicleRepository.Query().Include(v => v.User);

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(v =>
                v.Plate.Contains(term) ||
                v.Make.Contains(term) ||
                v.Model.Contains(term));
        }
        if (userId != null) query = query.Where(v => v.UserId == userId);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(v => v.Id).Paginate(page).ToListAsync();

        var rows = items
            .Select(v => new VehicleRow(v)
            {
                OwnerName = v.User != null ? $"{v.User.FirstName} {v.User.LastName}".Trim() : null
            })
            .ToList();

        return new BaseResponse<PageOutput<VehicleRow>>(new PageOutput<VehicleRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<VehicleRow>> Get(int id)
    {
        var vehicle = await vehicleRepository.Query().Include(v => v.User).FirstOrDefaultAsync(v => v.Id == id);
        if (vehicle == null) return new BaseResponse<VehicleRow>(default, ErrorCode.NotFound);
        return new BaseResponse<VehicleRow>(BuildRow(vehicle));
    }

    public async Task<BaseResponse<VehicleRow>> Create(VehicleInput input)
    {
        var vehicle = new Vehicle();
        Apply(vehicle, input);
        vehicleRepository.Create(vehicle);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.vehicles.create", nameof(Vehicle), vehicle.Id);
        return new BaseResponse<VehicleRow>(BuildRow(vehicle));
    }

    public async Task<BaseResponse<VehicleRow>> Update(int id, VehicleInput input)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(id);
        if (vehicle == null) return new BaseResponse<VehicleRow>(default, ErrorCode.NotFound);

        Apply(vehicle, input);
        vehicleRepository.Update(vehicle);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.vehicles.update", nameof(Vehicle), vehicle.Id);
        return new BaseResponse<VehicleRow>(BuildRow(vehicle));
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(id);
        if (vehicle == null) return new BaseResponse(ErrorCode.NotFound);

        vehicleRepository.SoftDelete(vehicle);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.vehicles.delete", nameof(Vehicle), id);
        return new BaseResponse();
    }

    private static void Apply(Vehicle vehicle, VehicleInput input)
    {
        vehicle.UserId = input.UserId;
        vehicle.Make = input.Make?.Trim() ?? string.Empty;
        vehicle.Model = input.Model?.Trim() ?? string.Empty;
        vehicle.Plate = input.Plate?.Trim() ?? string.Empty;
        vehicle.Color = input.Color;
        vehicle.Year = input.Year;
        vehicle.SeatCapacity = input.SeatCapacity;
        vehicle.PhotoUrl = input.PhotoUrl;
        vehicle.IsDefault = input.IsDefault;
    }

    private static VehicleRow BuildRow(Vehicle vehicle)
        => new VehicleRow(vehicle)
        {
            OwnerName = vehicle.User != null ? $"{vehicle.User.FirstName} {vehicle.User.LastName}".Trim() : null
        };
}
