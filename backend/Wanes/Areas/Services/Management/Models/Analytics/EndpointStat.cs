namespace Wanes.Areas.Services.Management.Models.Analytics;

/// <summary>Aggregated traffic for a single API route within the window.</summary>
public class EndpointStat
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Calls { get; set; }
    public double AvgDurationMs { get; set; }
    public long MaxDurationMs { get; set; }
    public int Errors { get; set; }
}
