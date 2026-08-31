namespace Wanes.Areas.Services.Search;

/// <summary>
/// How the rider wants carpool matches ordered.
///
/// Applied inside the query, before the result cap, so the page that comes back
/// is the best twenty <em>for that sort</em> rather than the twenty nearest
/// re-shuffled on the client. Every sort tie-breaks on <see cref="Best"/>, which
/// keeps the order stable across identical requests.
/// </summary>
public enum SearchSort
{
    /// <summary>Combined proximity of both ends — the default ranking.</summary>
    Best = 1,

    /// <summary>Leaving soonest first.</summary>
    Departure = 2,

    /// <summary>Cheapest seat first; trips with no price set come last.</summary>
    Price = 3,

    /// <summary>Highest-rated driver first.</summary>
    Rating = 4,

    /// <summary>Shortest walk to the pickup point, ignoring the far end.</summary>
    Pickup = 5,

    /// <summary>Most seats still free — what a rider booking for a group wants.</summary>
    Seats = 6,
}
