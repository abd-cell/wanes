using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Vehicles.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Vehicles;

public class VehicleService : IVehicleService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IRepository<User> userRepository;

    public VehicleService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        IRepository<Vehicle> vehicleRepository,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.vehicleRepository = vehicleRepository;
        this.userRepository = userRepository;
    }

    public async Task<BaseResponse<List<VehicleOutput>>> GetUserVehicles()
    {
        var userId = securityManager.RequireUserId();
        var vehicles = await vehicleRepository
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.IsDefault).ThenBy(v => v.Id)
            .ToListAsync();

        var data = vehicles.Select(v => new VehicleOutput(v)).ToList();
        return new BaseResponse<List<VehicleOutput>>(data);
    }

    public async Task<BaseResponse<VehicleOutput>> Create(VehicleInput input)
    {
        var userId = securityManager.RequireUserId();
        if (input.SeatCapacity < 1)
            return new BaseResponse<VehicleOutput>(default, ErrorCode.ValidationError, "Seat capacity must be at least 1.");

        var isFirst = !await vehicleRepository.AnyAsync(v => v.UserId == userId);

        var vehicle = new Vehicle
        {
            UserId = userId,
            Make = input.Make.Trim(),
            Model = input.Model.Trim(),
            Plate = input.Plate.Trim(),
            Color = input.Color,
            Year = input.Year,
            SeatCapacity = input.SeatCapacity,
            PhotoUrl = input.PhotoUrl,
            IsDefault = input.IsDefault || isFirst,
        };

        if (vehicle.IsDefault) await ClearDefault(userId);
        vehicleRepository.Create(vehicle);

        // adding a vehicle flags the user as a driver (pending verification)
        var user = await userRepository.GetByIdAsync(userId);
        if (user != null && !user.IsDriver)
        {
            user.IsDriver = true;
            if (user.DriverStatus == DriverStatus.None) user.DriverStatus = DriverStatus.Pending;
            userRepository.Update(user);
        }

        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.VehicleAdd, nameof(Vehicle), vehicle.Id);
        return new BaseResponse<VehicleOutput>(new VehicleOutput(vehicle));
    }

    public async Task<BaseResponse<VehicleOutput>> Update(int id, VehicleInput input)
    {
        var userId = securityManager.RequireUserId();
        var vehicle = vehicleRepository.FirstOrDefault(v => v.Id == id && v.UserId == userId);
        if (vehicle == null)
            return new BaseResponse<VehicleOutput>(default, ErrorCode.VehicleNotFound);

        vehicle.Make = input.Make.Trim();
        vehicle.Model = input.Model.Trim();
        vehicle.Plate = input.Plate.Trim();
        vehicle.Color = input.Color;
        vehicle.Year = input.Year;
        if (input.SeatCapacity >= 1) vehicle.SeatCapacity = input.SeatCapacity;
        vehicle.PhotoUrl = input.PhotoUrl;

        if (input.IsDefault && !vehicle.IsDefault)
        {
            await ClearDefault(userId);
            vehicle.IsDefault = true;
        }

        vehicleRepository.Update(vehicle);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.VehicleUpdate, nameof(Vehicle), vehicle.Id);
        return new BaseResponse<VehicleOutput>(new VehicleOutput(vehicle));
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var userId = securityManager.RequireUserId();
        var vehicle = vehicleRepository.FirstOrDefault(v => v.Id == id && v.UserId == userId);
        if (vehicle == null) return new BaseResponse(ErrorCode.VehicleNotFound);

        vehicleRepository.SoftDelete(vehicle);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.VehicleDelete, nameof(Vehicle), id);
        return new BaseResponse();
    }

    private async Task ClearDefault(int userId)
    {
        var defaults = await vehicleRepository
            .Where(v => v.UserId == userId && v.IsDefault).ToListAsync();
        foreach (var vehicle in defaults)
        {
            vehicle.IsDefault = false;
            vehicleRepository.Update(vehicle);
        }
    }
}
