namespace Wanes.Areas.Services.Management.Models.Analytics;

/// <summary>Ranked "who and where" tables for the window.</summary>
public class LeaderboardsOutput
{
    public List<LeaderRow> TopDrivers { get; set; } = [];
    public List<LeaderRow> TopRiders { get; set; } = [];
    public List<LeaderRow> TopRoutes { get; set; } = [];
    public List<LeaderRow> TopOrigins { get; set; } = [];
    public List<LeaderRow> TopDestinations { get; set; } = [];
    public List<LeaderRow> TopRatedDrivers { get; set; } = [];
}
