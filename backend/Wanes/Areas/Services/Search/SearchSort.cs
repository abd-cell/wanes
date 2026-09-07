namespace Wanes.Areas.Services.Search;

/// <summary>
/// How the rider wants carpool matches ordered.
///
/// Applied before the result cap, so the page that comes back is the best twenty
/// <em>for that sort</em> rather than the twenty nearest re-shuffled on the
/// client. The explicit sorts run inside the query; <see cref="Best"/> ranks a
/// pool in memory because it weighs time against distance. Every sort tie-breaks
/// on proximity and then departure, which keeps the order stable across
/// identical requests.
/// </summary>
public enum SearchSort
{
    /// <summary>
    /// Walk plus wait — the default. Combined proximity of both ends *and*
    /// closeness to the hour the rider asked for, weighed against each other so
    /// a trip leaving soon beats a nearer one leaving tomorrow without hiding
    /// tomorrow's. Ranked in memory rather than in the query, because the two
    /// halves cannot be scored together in SQL.
    /// </summary>
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
