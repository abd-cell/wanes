using System.Diagnostics;
using Wanes.Areas.Domain.Logging;
using Wanes.Areas.Services.Logging;
using Wanes.Shareds.Security;

namespace Wanes.Shareds.Middlewares;

/// <summary>
/// Records one <see cref="ApiLog"/> per API request (method, path, status,
/// duration, actor). Runs after authentication so the acting user is known.
/// Only <c>/api/</c> paths are logged; the log-checker endpoints and the SSE
/// stream are skipped to avoid noise / never-ending durations. Logging failures
/// are swallowed — diagnostics must never break a real request.
/// </summary>
public class ApiLoggerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiLoggerMiddleware> _logger;

    public ApiLoggerMiddleware(RequestDelegate next, ILogger<ApiLoggerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!ShouldLog(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            await SafeRecord(context, sw.ElapsedMilliseconds);
        }
    }

    private static bool ShouldLog(PathString path)
    {
        if (!path.StartsWithSegments("/api")) return false;
        // Don't log the checker reading its own logs, or the long-lived SSE stream.
        if (path.StartsWithSegments("/api/v1/api-logs")) return false;
        if (path.Value?.Contains("/stream", StringComparison.OrdinalIgnoreCase) == true) return false;
        return true;
    }

    private async Task SafeRecord(HttpContext context, long elapsedMs)
    {
        try
        {
            int? actor = null;
            if (int.TryParse(context.User.FindFirst(AppClaims.UserId)?.Value, out var uid))
                actor = uid;

            var log = new ApiLog
            {
                Method = Trunc(context.Request.Method, 10),
                Path = Trunc(context.Request.Path.Value ?? string.Empty, 400),
                QueryString = context.Request.QueryString.Value is { Length: > 0 } qs
                    ? Trunc(qs, 1000)
                    : null,
                StatusCode = context.Response.StatusCode,
                DurationMs = elapsedMs,
                ActorUserId = actor,
                Ip = context.Connection.RemoteIpAddress?.ToString() is { Length: > 0 } ip
                    ? Trunc(ip, 64)
                    : null,
                UserAgent = context.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua
                    ? Trunc(ua, 400)
                    : null,
            };

            var service = context.RequestServices.GetRequiredService<IApiLogService>();
            await service.RecordAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write API log");
        }
    }

    private static string Trunc(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
