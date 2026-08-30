namespace Wanes.Areas.Services.Management.Models.Analytics;

/// <summary>
/// Categorical distributions. Every list is complete — enum values with no rows are
/// returned as zero so the chart legend stays stable as the data changes.
/// </summary>
public class BreakdownsOutput
{
    public List<MetricPoint> TripsByStatus { get; set; } = [];
    public List<MetricPoint> BookingsByStatus { get; set; } = [];
    public List<MetricPoint> RequestsByStatus { get; set; } = [];
    public List<MetricPoint> UsersByDriverStatus { get; set; } = [];
    public List<MetricPoint> UsersByLanguage { get; set; } = [];
    public List<MetricPoint> UsersByGender { get; set; } = [];
    public List<MetricPoint> RatingsByStars { get; set; } = [];
    public List<MetricPoint> NotificationsByType { get; set; } = [];
    public List<MetricPoint> SessionsByDevice { get; set; } = [];
    public List<MetricPoint> PlacesByLabel { get; set; } = [];

    /// <summary>Demand (bookings + ride requests) per hour of day, 0–23.</summary>
    public List<MetricPoint> DemandByHour { get; set; } = [];

    /// <summary>Trip departures per weekday, key 0 = Sunday.</summary>
    public List<MetricPoint> TripsByWeekday { get; set; } = [];
}
