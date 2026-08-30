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

public class AdminUserLoginService : IAdminUserLoginService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<UserLogin> userLoginRepository;

    public AdminUserLoginService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<UserLogin> userLoginRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.userLoginRepository = userLoginRepository;
    }

    public async Task<BaseResponse<PageOutput<UserLoginRow>>> List(PageInput page, DeviceType? deviceType, int? userId)
    {
        IQueryable<UserLogin> query = userLoginRepository.Query().Include(l => l.User);

        if (deviceType != null) query = query.Where(l => l.DeviceType == deviceType);
        if (userId != null) query = query.Where(l => l.UserId == userId);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(l => l.Id).Paginate(page).ToListAsync();

        var rows = items.Select(l => new UserLoginRow(l)
        {
            OwnerName = l.User != null ? $"{l.User.FirstName} {l.User.LastName}".Trim() : null
        }).ToList();

        return new BaseResponse<PageOutput<UserLoginRow>>(new PageOutput<UserLoginRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<UserLoginRow>> Get(int id)
    {
        var login = await userLoginRepository.Query().Include(l => l.User).FirstOrDefaultAsync(l => l.Id == id);
        if (login == null) return new BaseResponse<UserLoginRow>(default, ErrorCode.NotFound);
        return new BaseResponse<UserLoginRow>(new UserLoginRow(login)
        {
            OwnerName = login.User != null ? $"{login.User.FirstName} {login.User.LastName}".Trim() : null
        });
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var login = await userLoginRepository.GetByIdAsync(id);
        if (login == null) return new BaseResponse(ErrorCode.NotFound);

        userLoginRepository.SoftDelete(login);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.sessions.delete", nameof(UserLogin), id);
        return new BaseResponse();
    }
}
