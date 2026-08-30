using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

public class AdminUserService : IAdminUserService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<UserRole> userRoleRepository;

    public AdminUserService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<User> userRepository,
        IRepository<UserRole> userRoleRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.userRepository = userRepository;
        this.userRoleRepository = userRoleRepository;
    }

    public async Task<BaseResponse<PageOutput<UserRow>>> List(PageInput page, DriverStatus? driverStatus, bool? isDriver, bool? isDisabled)
    {
        var query = userRepository.Query();

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(u =>
                u.Phone.Contains(term) ||
                u.FirstName.Contains(term) ||
                u.LastName.Contains(term) ||
                (u.Email != null && u.Email.Contains(term)));
        }
        if (driverStatus != null) query = query.Where(u => u.DriverStatus == driverStatus);
        if (isDriver != null) query = query.Where(u => u.IsDriver == isDriver);
        if (isDisabled != null) query = query.Where(u => u.IsDisabled == isDisabled);

        var total = await query.CountAsync();
        var users = await query.OrderByDescending(u => u.Id).Paginate(page).ToListAsync();

        var ids = users.Select(u => u.Id).ToList();
        var roleMap = await userRoleRepository.Where(r => ids.Contains(r.UserId))
            .GroupBy(r => r.UserId)
            .Select(g => new { g.Key, Roles = g.Select(x => x.Role).ToList() })
            .ToDictionaryAsync(x => x.Key, x => x.Roles);

        var rows = users
            .Select(u => new UserRow(u, roleMap.TryGetValue(u.Id, out var r) ? r : []))
            .ToList();

        return new BaseResponse<PageOutput<UserRow>>(new PageOutput<UserRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<UserRow>> Get(int id)
    {
        var user = await userRepository.GetByIdAsync(id);
        if (user == null) return new BaseResponse<UserRow>(default, ErrorCode.NotFound);
        return new BaseResponse<UserRow>(await BuildRow(user));
    }

    public async Task<BaseResponse<UserRow>> Create(UserInput input)
    {
        var phone = input.Phone.NormalizePhone();
        if (string.IsNullOrWhiteSpace(phone))
            return new BaseResponse<UserRow>(default, ErrorCode.ValidationError, "Phone is required.");
        // Sign-in resolves accounts by key, so the key — not the written form — is what must be unique.
        var phoneKey = phone.PhoneKey();
        if (await userRepository.AnyAsync(u => u.PhoneKey == phoneKey))
            return new BaseResponse<UserRow>(default, ErrorCode.PhoneAlreadyRegistered);

        var user = new User { Phone = phone, PhoneKey = phoneKey };
        Apply(user, input);
        userRepository.Create(user);
        await unitOfWork.SaveAsync();

        // every account holds at least the base User role
        userRoleRepository.Create(new UserRole { UserId = user.Id, Role = Roles.User });
        await unitOfWork.SaveAsync();

        await auditService.LogAsync("admin.user.create", nameof(User), user.Id);
        return new BaseResponse<UserRow>(await BuildRow(user));
    }

    public async Task<BaseResponse<UserRow>> Update(int id, UserInput input)
    {
        var user = await userRepository.GetByIdAsync(id);
        if (user == null) return new BaseResponse<UserRow>(default, ErrorCode.NotFound);

        var phone = input.Phone.NormalizePhone();
        if (!string.IsNullOrWhiteSpace(phone) && phone != user.Phone)
        {
            var phoneKey = phone.PhoneKey();
            if (await userRepository.AnyAsync(u => u.PhoneKey == phoneKey && u.Id != id))
                return new BaseResponse<UserRow>(default, ErrorCode.PhoneAlreadyRegistered);
            user.Phone = phone;
            user.PhoneKey = phoneKey;
        }

        Apply(user, input);
        userRepository.Update(user);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.user.update", nameof(User), user.Id);
        return new BaseResponse<UserRow>(await BuildRow(user));
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var user = await userRepository.GetByIdAsync(id);
        if (user == null) return new BaseResponse(ErrorCode.NotFound);

        userRepository.SoftDelete(user);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.user.delete", nameof(User), id);
        return new BaseResponse();
    }

    public async Task<BaseResponse<UserRow>> SetRole(int id, RoleInput input)
    {
        var user = await userRepository.GetByIdAsync(id);
        if (user == null) return new BaseResponse<UserRow>(default, ErrorCode.NotFound);

        var existing = userRoleRepository.FirstOrDefault(r => r.UserId == id && r.Role == input.Role);
        if (input.Grant && existing == null)
        {
            userRoleRepository.Create(new UserRole { UserId = id, Role = input.Role });
        }
        else if (!input.Grant && existing != null)
        {
            userRoleRepository.SoftDelete(existing);
        }
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(input.Grant ? "admin.user.role_grant" : "admin.user.role_revoke", nameof(User), id);
        return new BaseResponse<UserRow>(await BuildRow(user));
    }

    private static void Apply(User user, UserInput input)
    {
        user.Email = input.Email;
        user.FirstName = input.FirstName?.Trim() ?? string.Empty;
        user.LastName = input.LastName?.Trim() ?? string.Empty;
        user.DisplayName = input.DisplayName;
        user.Gender = input.Gender;
        user.IsRider = input.IsRider;
        user.IsDriver = input.IsDriver;
        user.DriverStatus = input.DriverStatus;
        user.LicenseNumber = input.LicenseNumber;
        user.Language = input.Language;
        user.IsDisabled = input.IsDisabled;
    }

    private async Task<UserRow> BuildRow(User user)
    {
        var roles = await userRoleRepository.Where(r => r.UserId == user.Id)
            .Select(r => r.Role).ToListAsync();
        return new UserRow(user, roles);
    }
}
