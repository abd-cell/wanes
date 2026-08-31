using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Search.Models;

public class SearchInput
{
    public GeoPoint Origin { get; set; } = new();
    public GeoPoint Destination { get; set; } = new();
    public DateTime When { get; set; } = DateTime.UtcNow;
    public int Seats { get; set; } = 1;

    /// <summary>
    /// Narrow match (5 km around each end) vs. wide (50 km). Riders default to
    /// narrow — a wide match usually means a long walk at one end of the trip.
    /// </summary>
    public bool Nearby { get; set; } = true;

    /// <summary>
    /// Which order the matches come back in. Defaults to the combined-proximity
    /// ranking; the app sends back whatever the rider last chose.
    /// </summary>
    public SearchSort SortBy { get; set; } = SearchSort.Best;
}
