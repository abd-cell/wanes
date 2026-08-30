using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Logging;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

public class AdminApiLogService : IAdminApiLogService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<ApiLog> apiLogRepository;
    private readonly IRepository<User> userRepository;

    public AdminApiLogService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<ApiLog> apiLogRepository,
        IRepository<User> userRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.apiLogRepository = apiLogRepository;
        this.userRepository = userRepository;
    }

    public async Task<BaseResponse<PageOutput<ApiLogRow>>> List(PageInput page, int? statusCode, string? method)
    {
        var query = apiLogRepository.Query();

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(l => l.Path.Contains(term));
        }
        if (statusCode != null) query = query.Where(l => l.StatusCode == statusCode);
        if (method != null) query = query.Where(l => l.Method == method);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(l => l.Id).Paginate(page).ToListAsync();

        var names = await userRepository.ResolveNames(items.Select(l => l.ActorUserId));
        var rows = items.Select(l => new ApiLogRow(l) { ActorName = names.NameFor(l.ActorUserId) }).ToList();

        return new BaseResponse<PageOutput<ApiLogRow>>(new PageOutput<ApiLogRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<ApiLogRow>> Get(int id)
    {
        var log = await apiLogRepository.GetByIdAsync(id);
        if (log == null) return new BaseResponse<ApiLogRow>(default, ErrorCode.NotFound);

        var names = await userRepository.ResolveNames([log.ActorUserId]);
        return new BaseResponse<ApiLogRow>(new ApiLogRow(log) { ActorName = names.NameFor(log.ActorUserId) });
    }
}
