using Wanes.Areas.Domain.Logging;
using Wanes.Areas.Services.Logging.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Logging;

[ScopedInjectable]
public interface IApiLogService
{
    /// <summary>Persists one API-log row. Never throws into the caller's flow.</summary>
    Task RecordAsync(ApiLog log);

    /// <summary>Paged, newest-first listing with optional filters (the "checker").</summary>
    Task<BaseResponse<PageOutput<ApiLogRow>>> GetLogs(
        PageInput page, string? method, string? path, int? statusCode, int? actorUserId);

    /// <summary>Aggregate health snapshot over the most recent <paramref name="sampleSize"/> rows.</summary>
    Task<BaseResponse<ApiLogSummary>> GetSummary(int sampleSize);
}
