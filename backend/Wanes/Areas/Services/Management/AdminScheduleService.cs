using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Schedules;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

public class AdminScheduleService : IAdminScheduleService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<TripSchedule> scheduleRepository;

    public AdminScheduleService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<TripSchedule> scheduleRepository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.scheduleRepository = scheduleRepository;
    }

    public async Task<BaseResponse<PageOutput<ScheduleRow>>> List(
        PageInput page, ActiveRole? ownerRole, int? ownerId)
    {
        IQueryable<TripSchedule> query = scheduleRepository.Query().Include(s => s.Owner);

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(s =>
                s.OriginAddress.Contains(term) ||
                s.DestinationAddress.Contains(term));
        }
        if (ownerRole != null) query = query.Where(s => s.OwnerRole == ownerRole);
        if (ownerId != null) query = query.Where(s => s.OwnerId == ownerId);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(s => s.Id).Paginate(page).ToListAsync();

        var rows = items.Select(s => new ScheduleRow(s) { OwnerName = AdminLabels.ForUser(s.Owner) })
            .ToList();

        return new BaseResponse<PageOutput<ScheduleRow>>(
            new PageOutput<ScheduleRow> { TotalRows = total, Data = rows });
    }

    public async Task<BaseResponse<ScheduleRow>> Get(int id)
    {
        var schedule = await scheduleRepository.Query()
            .Include(s => s.Owner)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (schedule == null) return new BaseResponse<ScheduleRow>(default, ErrorCode.ScheduleNotFound);

        return new BaseResponse<ScheduleRow>(
            new ScheduleRow(schedule) { OwnerName = AdminLabels.ForUser(schedule.Owner) });
    }

    public async Task<BaseResponse<ScheduleRow>> Update(int id, ScheduleInput input)
    {
        var schedule = await scheduleRepository.GetByIdAsync(id);
        if (schedule == null) return new BaseResponse<ScheduleRow>(default, ErrorCode.ScheduleNotFound);

        var before = new ScheduleRow(schedule);

        schedule.IsPaused = input.IsPaused;
        // Nullable so an admin pausing a series does not have to re-send its
        // time and end date to avoid blanking them.
        if (input.TimeOfDay != null) schedule.TimeOfDay = input.TimeOfDay.Value;
        if (input.EndDate != null) schedule.EndDate = input.EndDate;

        scheduleRepository.Update(schedule);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.schedules.update", nameof(TripSchedule), schedule.Id,
            before, new ScheduleRow(schedule));

        return await Get(schedule.Id);
    }

    /// <summary>
    /// Removes the schedule. Occurrences it already wrote are left exactly as
    /// they are — riders may be on them, and the owner's own delete is the path
    /// that cleans up the unbooked ones.
    /// </summary>
    public async Task<BaseResponse> Delete(int id)
    {
        var schedule = await scheduleRepository.GetByIdAsync(id);
        if (schedule == null) return new BaseResponse(ErrorCode.ScheduleNotFound);

        scheduleRepository.SoftDelete(schedule);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.schedules.delete", nameof(TripSchedule), id);

        return new BaseResponse();
    }
}
