using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Logging;

/// <summary>
/// Append-only record of a single HTTP API request/response. Written by
/// <c>ApiLoggerMiddleware</c> after the response is produced. Diagnostic in
/// nature (who called what, how long, what status) — distinct from the business
/// <see cref="Audit.AuditLog"/> which captures mutating domain actions.
/// </summary>
public class ApiLog : BaseEntity
{
    public string Method { get; set; } = string.Empty;   // GET, POST, ...
    public string Path { get; set; } = string.Empty;      // /api/v1/trips
    public string? QueryString { get; set; }

    public int StatusCode { get; set; }
    public long DurationMs { get; set; }

    public int? ActorUserId { get; set; }                 // null when unauthenticated
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}
