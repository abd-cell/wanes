namespace Wanes.Areas.Services.Management.Models.Analytics;

/// <summary>One day of a daily time series. Gaps are filled with zero by the service.</summary>
public class SeriesPoint
{
    public DateTime Date { get; set; }
    public int Value { get; set; }
}
