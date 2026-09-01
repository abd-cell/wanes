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

    /// <summary>
    /// When the opened hail stops being answerable (Hail mode).
    ///
    /// Sent so the rider's countdown runs on the deadline the server actually
    /// stamped. The window is admin-set, so a client that added its own idea of
    /// the TTL to "now" would drift from the server the moment an admin changed
    /// it — and would keep telling the rider to wait after the hail had closed.
    /// </summary>
    public DateTime? RideRequestExpiresAt { get; set; }
}
