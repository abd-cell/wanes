namespace Wanes.Shareds.Constants;

/// <summary>
/// The one place the rider↔driver matching envelope is defined. Search, the
/// hail push and the reverse match a driver triggers by posting a trip all read
/// from here — when these numbers lived in three services they drifted, and a
/// rider could open a 50 km hail that only ever reached drivers 2 km away.
/// </summary>
public static class MatchRules
{
    /// <summary>Rider chose "Nearby": both ends stay walkable.</summary>
    public const int NearRadiusMeters = 5000;

    /// <summary>Rider chose "Anywhere": intercity, at the cost of a longer walk.</summary>
    public const int WideRadiusMeters = 50000;

    /// <summary>How far either side of the wanted departure a trip still counts as a match.</summary>
    public static readonly TimeSpan TimeWindow = TimeSpan.FromMinutes(30);

    /// <summary>How long an unanswered hail stays open for drivers to pick up.</summary>
    public static readonly TimeSpan HailTtl = TimeSpan.FromMinutes(10);

    public static int RadiusFor(bool nearby) => nearby ? NearRadiusMeters : WideRadiusMeters;
}
