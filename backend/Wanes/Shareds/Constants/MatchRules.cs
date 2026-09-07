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

    /// <summary>
    /// How far either side of a wanted departure two rides count as the same
    /// moment.
    ///
    /// Note what this is *not*: search does not filter on it. A trip with free
    /// seats that has not departed is discoverable whenever it leaves, and the
    /// rider's wanted time only ranks it (<see cref="RankTimeScale"/>). Used as
    /// a hard window, this quietly hid a perfectly good trip two hours out and
    /// sent the rider off to hail instead.
    ///
    /// What still reads it: the driver clash window (two of one driver's
    /// departures this close compete for the same driver) and the reverse-match
    /// push (a rider is told about a *new* trip only if it serves roughly the
    /// hour they asked for — an unprompted push about next week is spam, which
    /// is a different question from what a search may show).
    /// </summary>
    public static readonly TimeSpan TimeWindow = TimeSpan.FromMinutes(30);

    /// <summary>
    /// In the default ranking, the time gap that costs a trip as much as being
    /// a full match radius away at both ends.
    ///
    /// Search ranks on walk *and* wait together, so the two have to be weighed
    /// against each other in some unit. An hour off the wanted departure is
    /// worth about as much as a long walk at both ends — which keeps a trip
    /// leaving soon in front of one leaving tomorrow from next door, without
    /// excluding tomorrow's.
    /// </summary>
    public static readonly TimeSpan RankTimeScale = TimeSpan.FromMinutes(60);

    /// <summary>
    /// How many candidates the default ranking weighs before capping the page.
    ///
    /// The database can order on one end or the other but not on the combined
    /// walk-and-wait score — that needs real distances, and the geography
    /// operators only answer in metres inside a query. So a pool comes back
    /// ordered by proximity and is ranked properly in memory. Wide enough that
    /// the trip a rider actually wants is inside it; capped because "every
    /// future trip on this route" has no natural ceiling.
    /// </summary>
    public const int CandidatePool = 200;

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

    /// <summary>
    /// How recently a driver must have reported a position for it to count as
    /// where they are.
    ///
    /// A driver only reports while the app is running, so most posted trips have
    /// a stale fix or none — which is why a missing or old one is *ignored*
    /// rather than disqualifying (search falls back to the trip's planned
    /// origin). Half an hour because a driver who was reporting that recently is
    /// plausibly still in the same part of town; much longer and "live" stops
    /// meaning anything, much shorter and only a driver with the app open in
    /// their hand would ever qualify.
    /// </summary>
    public static readonly TimeSpan LiveFixWindow = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The cutoff a reported position must beat to count as current. Queries
    /// compare against this rather than calling <see cref="IsLiveFix"/>, which
    /// cannot be translated to SQL — same window either way.
    /// </summary>
    public static DateTime LiveFixFloor(DateTime now) => now - LiveFixWindow;

    /// <summary>
    /// Whether a position reported at <paramref name="reportedAt"/> still says
    /// where the driver is. A driver who has never reported has no live fix, and
    /// neither does one whose position carries no timestamp — an unaged position
    /// cannot be called current, so it does not get to exclude a trip.
    /// </summary>
    public static bool IsLiveFix(DateTime? reportedAt, DateTime now) =>
        reportedAt != null && reportedAt > LiveFixFloor(now);

    public static int RadiusFor(bool nearby) => nearby ? NearRadiusMeters : WideRadiusMeters;
}
