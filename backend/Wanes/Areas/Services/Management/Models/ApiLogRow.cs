using Wanes.Areas.Domain.Logging;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>Admin view of a single API log entry (list + detail share one shape).</summary>
public class ApiLogRow
{
    public int Id { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public int? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreationDate { get; set; }

    public ApiLogRow() { }

    public ApiLogRow(ApiLog e)
    {
        Id = e.Id;
        Method = e.Method;
        Path = e.Path;
        QueryString = e.QueryString;
        StatusCode = e.StatusCode;
        DurationMs = e.DurationMs;
        ActorUserId = e.ActorUserId;
        Ip = e.Ip;
        UserAgent = e.UserAgent;
        CreationDate = e.CreationDate;
    }
}
