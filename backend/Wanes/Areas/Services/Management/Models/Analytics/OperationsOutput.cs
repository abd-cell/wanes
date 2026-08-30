namespace Wanes.Areas.Services.Management.Models.Analytics;

/// <summary>API health + audit activity for the window, from the request log.</summary>
public class OperationsOutput
{
    public int TotalCalls { get; set; }
    public int Ok2xx { get; set; }
    public int Redirect3xx { get; set; }
    public int ClientError4xx { get; set; }
    public int ServerError5xx { get; set; }
    public double AvgDurationMs { get; set; }
    public long MaxDurationMs { get; set; }
    /// <summary>Calls slower than one second.</summary>
    public int SlowCalls { get; set; }
    public int UnauthenticatedCalls { get; set; }

    public List<SeriesPoint> CallsByDay { get; set; } = [];
    public List<SeriesPoint> ErrorsByDay { get; set; } = [];
    public List<EndpointStat> TopEndpoints { get; set; } = [];
    public List<EndpointStat> SlowestEndpoints { get; set; } = [];
    public List<LeaderRow> TopAuditActions { get; set; } = [];
    public List<SeriesPoint> AuditByDay { get; set; } = [];
}
