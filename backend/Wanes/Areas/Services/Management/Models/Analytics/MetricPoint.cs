namespace Wanes.Areas.Services.Management.Models.Analytics;

/// <summary>
/// One slice of a categorical breakdown. <see cref="Key"/> is the enum value (or the
/// bucket number) so the CMS can resolve its own translated label; <see cref="Label"/>
/// is the raw name as a fallback.
/// </summary>
public class MetricPoint
{
    public int Key { get; set; }
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
}
