namespace Wanes.Areas.Services.Management.Models.Analytics;

/// <summary>Daily activity series over the window. Every list has one point per day.</summary>
public class TimeSeriesOutput
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }

    public List<SeriesPoint> NewUsers { get; set; } = [];
    public List<SeriesPoint> NewTrips { get; set; } = [];
    public List<SeriesPoint> NewBookings { get; set; } = [];
    public List<SeriesPoint> NewRiderTrips { get; set; } = [];
    public List<SeriesPoint> CompletedTrips { get; set; } = [];
    public List<SeriesPoint> CancelledTrips { get; set; } = [];
    public List<SeriesPoint> Logins { get; set; } = [];
    public List<SeriesPoint> SeatsBooked { get; set; } = [];
}
