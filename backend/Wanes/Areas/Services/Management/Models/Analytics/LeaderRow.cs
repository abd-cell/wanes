namespace Wanes.Areas.Services.Management.Models.Analytics;

/// <summary>A ranked row: top drivers, riders, or routes.</summary>
public class LeaderRow
{
    public int? Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Sublabel { get; set; }
    public int Value { get; set; }
    public double? Secondary { get; set; }
}
