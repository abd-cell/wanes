namespace Wanes.Areas.Services.Search;

/// <summary>
/// How a trip came to be a match — the band it sits in, not a score.
///
/// The home page renders these as sections in this order, and the ranking
/// orders on this before anything else, so the bands cannot interleave whatever
/// the rider sorted by. A tier is never a relaxation of the rules: a trip in the
/// second band is as bookable as one in the first, it just asks the driver to
/// stop somewhere they had not planned.
/// </summary>
public enum SearchTier
{
    /// <summary>
    /// The trip's own two ends are near the rider's. They are going where the
    /// rider is going, and the walk is to a point the driver already meant to
    /// be at.
    /// </summary>
    Direct = 1,

    /// <summary>
    /// The trip's route passes the rider — both of their ends lie in the
    /// corridor around it, in the driver's own direction of travel.
    /// </summary>
    OnTheWay = 2,
}
