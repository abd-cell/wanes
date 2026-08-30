namespace Wanes.Areas.Services.Logging.Models;

/// <summary>Read model for the API-log checker listing.</summary>
public class ApiLogRow
{
    public int Id { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public int? ActorUserId { get; set; }
    public string? Ip { get; set; }
    public DateTime CreationDate { get; set; }
}

/// <summary>Aggregate "is the API healthy" snapshot over the recent window.</summary>
public class ApiLogSummary
{
    public int TotalRequests { get; set; }
    public int ClientErrors { get; set; }   // 4xx
    public int ServerErrors { get; set; }   // 5xx
    public double AvgDurationMs { get; set; }
    public long MaxDurationMs { get; set; }
    public DateTime? OldestAt { get; set; }
    public DateTime? NewestAt { get; set; }
}
