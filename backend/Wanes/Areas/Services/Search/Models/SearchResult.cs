using Wanes.Areas.Services.Trips.Models;

namespace Wanes.Areas.Services.Search.Models;

public class SearchResult
{
    public SearchMode Mode { get; set; }
    public List<TripOutput> Matches { get; set; } = [];

    /// <summary>Set when Mode = Hail — the opened ride request awaiting nearby drivers.</summary>
    public int? RideRequestId { get; set; }

    /// <summary>How many nearby drivers were notified (Hail mode).</summary>
    public int DriversNotified { get; set; }
}
