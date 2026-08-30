using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Audit;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

public class AdminService : IAdminService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IRepository<AuditLog> auditLogRepository;

    public AdminService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<User> userRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<AuditLog> auditLogRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.userRepository = userRepository;
        this.vehicleRepository = vehicleRepository;
        this.auditLogRepository = auditLogRepository;
    }

    public async Task<BaseResponse<PageOutput<DriverRow>>> GetPendingDrivers(PageInput page)
    {
        var query = userRepository.Where(u => u.DriverStatus == DriverStatus.Pending);

        var total = await query.CountAsync();
        var users = await query.OrderBy(u => u.Id).Paginate(page).ToListAsync();

        var rows = users.Select(u => new DriverRow
        {
            Id = u.Id,
            Phone = u.Phone,
            Name = AdminLabels.ForUserName(u),
            DriverStatus = u.DriverStatus,
            LicenseNumber = u.LicenseNumber,
        }).ToList();

        return new BaseResponse<PageOutput<DriverRow>>(new PageOutput<DriverRow>
        {
            TotalRows = total,
            Data = rows,
        });
    }

    public async Task<BaseResponse> VerifyDriver(int userId, VerifyInput input)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null) return new BaseResponse(ErrorCode.NotFound);

        user.DriverStatus = input.Approve ? DriverStatus.Verified : DriverStatus.Rejected;
        userRepository.Update(user);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(input.Approve ? AuditActions.AdminDriverVerified : AuditActions.AdminDriverRejected,
            nameof(User), userId);
        return new BaseResponse();
    }

    public async Task<BaseResponse<PageOutput<AuditRow>>> GetAuditLog(PageInput page, int? actorUserId, string? action)
    {
        var query = auditLogRepository.Query();
        if (actorUserId != null) query = query.Where(a => a.ActorUserId == actorUserId);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action.Contains(action));

        var total = await query.CountAsync();
        var logs = await query.OrderByDescending(a => a.Id).Paginate(page).ToListAsync();

        var names = await userRepository.ResolveNames(logs.Select(a => a.ActorUserId));

        var rows = logs.Select(a => new AuditRow
        {
            Id = a.Id,
            ActorUserId = a.ActorUserId,
            ActorName = names.NameFor(a.ActorUserId),
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            Ip = a.Ip,
            CreationDate = a.CreationDate,
        }).ToList();

        return new BaseResponse<PageOutput<AuditRow>>(new PageOutput<AuditRow>
        {
            TotalRows = total,
            Data = rows,
        });
    }
}
