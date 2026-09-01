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

    /// <summary>
    /// How long an unanswered hail stays open, when nobody has said otherwise.
    ///
    /// The live value is <c>AppConfiguration.HailRequestTtlMinutes</c>, which an
    /// admin sets from the CMS — this is what a fresh install starts with, and
    /// what a client falls back to before it has read the configuration.
    /// </summary>
    public const int DefaultHailTtlMinutes = 10;

    /// <summary>Bounds on the admin-set TTL. Below a minute no driver can answer;
    /// above four hours a forgotten hail keeps pinging drivers all afternoon.</summary>
    public const int MinHailTtlMinutes = 1;

    public const int MaxHailTtlMinutes = 240;

    /// <summary><see cref="DefaultHailTtlMinutes"/> as a span.</summary>
    public static readonly TimeSpan HailTtl = TimeSpan.FromMinutes(DefaultHailTtlMinutes);

    /// <summary>An admin-set TTL, clamped to something a hail can sanely live for.</summary>
    public static TimeSpan HailTtlFor(int minutes) =>
        TimeSpan.FromMinutes(Math.Clamp(minutes, MinHailTtlMinutes, MaxHailTtlMinutes));

    /// <summary>
    /// How far ahead a hail-accepted trip departs.
    ///
    /// A driver who takes a hail is not leaving this instant — they have to
    /// reach the pickup first. Stamping <c>DepartAt = UtcNow</c> made the row a
    /// lie and, worse, an invisible one: search only offers trips departing in
    /// the future, so the trip was already in the past by the time anyone
    /// looked, and no second rider could ever join it.
    /// </summary>
    public static readonly TimeSpan HailPickupLead = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long past its departure a trip that has not started is still
    /// offered — the rider is at the kerb, the driver is a few minutes late.
    ///
    /// Search cannot simply require a future departure. That excludes the
    /// hail-accepted trip above the moment its lead elapses, and it excludes
    /// every posted trip whose driver is running late but has not pressed
    /// start. Keeping the grace short is what still excludes the case it was
    /// written for: a trip from this morning that was never started at all.
    /// </summary>
    public static readonly TimeSpan BoardingGrace = TimeSpan.FromMinutes(15);

    /// <summary>
    /// When a hail wanted for <paramref name="wantedDepartAt"/> would actually leave.
    ///
    /// A hail for "now" still leaves after <see cref="HailPickupLead"/> — the
    /// driver has to reach the pickup. A hail for a scheduled time leaves at
    /// that time, which is the whole point of carrying the rider's wanted
    /// departure onto the request: a rider who searched for six this evening and
    /// found nothing is asking a driver to take them at six, not right now.
    ///
    /// The lead is a floor, not an offset, so a wanted time that has since
    /// slipped into the past — an old hail answered late — still produces a
    /// departure search can offer.
    /// </summary>
    public static DateTime HailDepartureFor(DateTime wantedDepartAt, DateTime now)
    {
        var earliest = now.Add(HailPickupLead);
        return wantedDepartAt > earliest ? wantedDepartAt : earliest;
    }

    public static int RadiusFor(bool nearby) => nearby ? NearRadiusMeters : WideRadiusMeters;
}
