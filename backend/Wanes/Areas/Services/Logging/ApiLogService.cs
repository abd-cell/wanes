using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Logging;
using Wanes.Areas.Services.Logging.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Logging;

public class ApiLogService : IApiLogService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IRepository<ApiLog> apiLogRepository;

    public ApiLogService(IUnitOfWork unitOfWork, IRepository<ApiLog> apiLogRepository)
    {
        this.unitOfWork = unitOfWork;
        this.apiLogRepository = apiLogRepository;
    }

    public async Task RecordAsync(ApiLog log)
    {
        await apiLogRepository.AddAsync(log);
        await unitOfWork.SaveAsync();
    }

    public async Task<BaseResponse<PageOutput<ApiLogRow>>> GetLogs(
        PageInput page, string? method, string? path, int? statusCode, int? actorUserId)
    {
        var query = apiLogRepository.Query();

        if (!string.IsNullOrWhiteSpace(method))
            query = query.Where(l => l.Method == method);
        if (!string.IsNullOrWhiteSpace(path))
            query = query.Where(l => l.Path.Contains(path));
        if (statusCode != null)
            query = query.Where(l => l.StatusCode == statusCode);
        if (actorUserId != null)
            query = query.Where(l => l.ActorUserId == actorUserId);

        var total = await query.CountAsync();
        var logs = await query.OrderByDescending(l => l.Id).Paginate(page).ToListAsync();

        var rows = logs.Select(l => new ApiLogRow
        {
            Id = l.Id,
            Method = l.Method,
            Path = l.Path,
            StatusCode = l.StatusCode,
            DurationMs = l.DurationMs,
            ActorUserId = l.ActorUserId,
            Ip = l.Ip,
            CreationDate = l.CreationDate,
        }).ToList();

        return new BaseResponse<PageOutput<ApiLogRow>>(new PageOutput<ApiLogRow>
        {
            TotalRows = total,
            Data = rows,
        });
    }

    public async Task<BaseResponse<ApiLogSummary>> GetSummary(int sampleSize)
    {
        if (sampleSize is < 1 or > 5000) sampleSize = 500;

        // Take the most recent N rows, then aggregate in memory (keeps the SQL simple).
        var recent = await apiLogRepository.Query()
            .OrderByDescending(l => l.Id)
            .Take(sampleSize)
            .Select(l => new { l.StatusCode, l.DurationMs, l.CreationDate })
            .ToListAsync();

        var summary = new ApiLogSummary
        {
            TotalRequests = recent.Count,
            ClientErrors = recent.Count(r => r.StatusCode is >= 400 and < 500),
            ServerErrors = recent.Count(r => r.StatusCode >= 500),
            AvgDurationMs = recent.Count == 0 ? 0 : Math.Round(recent.Average(r => r.DurationMs), 1),
            MaxDurationMs = recent.Count == 0 ? 0 : recent.Max(r => r.DurationMs),
            OldestAt = recent.Count == 0 ? null : recent.Min(r => r.CreationDate),
            NewestAt = recent.Count == 0 ? null : recent.Max(r => r.CreationDate),
        };

        return new BaseResponse<ApiLogSummary>(summary);
    }
}
