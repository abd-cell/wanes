namespace Wanes.Shareds.Constants;

/// <summary>
/// The one place the rider↔driver matching envelope is defined. Tiered search,
/// the driver's board of rider-posted trips and the reverse match a driver
/// triggers by posting a trip all read from here — when these numbers lived in
/// three services they drifted, and a rider could post a 50 km ask that only
/// ever reached drivers 2 km away.
///
/// What is *not* here: when a seat threshold has to be decided
/// (<c>Areas/Domain/Trips/TripConfirmationRules</c>), how far ahead
/// a rider may post (<c>Areas/Domain/RiderTrips/RiderTripRules</c>) and how a
/// recurrence unrolls (<c>Areas/Domain/Schedules/RecurrenceRules</c>). Those are
/// lifecycle rules that happen to involve time; these are about *reach*.
/// </summary>
public static class MatchRules
{
    /// <summary>Rider chose "Nearby": both ends stay walkable.</summary>
    public const int NearRadiusMeters = 5000;

    /// <summary>Rider chose "Anywhere": intercity, at the cost of a longer walk.</summary>
    public const int WideRadiusMeters = 50000;

    public static int RadiusFor(bool nearby) => nearby ? NearRadiusMeters : WideRadiusMeters;

    /// <summary>
    /// How far either side of a wanted departure two rides count as the same
    /// moment.
    ///
    /// Note what this is *not*: search does not filter on it. A trip with free
    /// seats that has not departed is discoverable whenever it leaves, and the
    /// rider's wanted time only ranks it (<see cref="RankTimeScale"/>). Used as
    /// a hard window, this quietly hid a perfectly good trip two hours out.
    ///
    /// What still reads it: the driver clash window (two of one driver's
    /// departures this close compete for the same driver), the reverse-match
    /// push (a rider is told about a *new* trip only if it serves roughly the
    /// hour they asked for — an unprompted push about next week is spam, which
    /// is a different question from what a search may show) and the immediate
    /// fan-out for a rider-posted trip leaving right away.
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
    /// operators only answer in metres *inside* a query. So a pool comes back
    /// ordered by proximity and is ranked properly in memory. Wide enough that
    /// the trip a rider actually wants is inside it; capped because "every
    /// future trip on this route" has no natural ceiling.
    /// </summary>
    public const int CandidatePool = 200;

    /// <summary>
    /// How far ahead a trip claimed from a rider-posted one departs, at the
    /// earliest.
    ///
    /// A driver who takes a posting is not leaving this instant — they have to
    /// reach the pickup. Stamping <c>DepartAt = UtcNow</c> made the row a lie
    /// and, worse, an invisible one: search only offers trips departing in the
    /// future, so the trip was already in the past by the time anyone looked,
    /// and no second rider could ever join it.
    /// </summary>
    public static readonly TimeSpan PickupLead = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long past its departure a trip that has not started is still
    /// offered — the rider is at the kerb, the driver is a few minutes late.
    ///
    /// Search cannot simply require a future departure. That excludes a
    /// just-claimed trip the moment its pickup lead elapses, and it excludes
    /// every posted trip whose driver is running late but has not pressed
    /// start. Keeping the grace short is what still excludes the case it was
    /// written for: a trip from this morning that was never started at all.
    /// </summary>
    public static readonly TimeSpan BoardingGrace = TimeSpan.FromMinutes(15);

    /// <summary>
    /// When a ride wanted for <paramref name="wantedDepartAt"/> would actually
    /// leave.
    ///
    /// A posting for "now" still leaves after <see cref="PickupLead"/> — the
    /// driver has to reach the pickup. One for a scheduled time leaves at that
    /// time, which is the whole point of a rider naming an hour: they are asking
    /// a driver to take them at six, not right now.
    ///
    /// The lead is a floor, not an offset, so a wanted time that has since
    /// slipped into the past — an old posting claimed late — still produces a
    /// departure search can offer.
    /// </summary>
    public static DateTime DepartureFor(DateTime wantedDepartAt, DateTime now)
    {
        var earliest = now.Add(PickupLead);
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

    // ── The corridor: trips that pass the rider's way ──

    /// <summary>
    /// How far off a trip's route the rider's own ends may sit and still count
    /// as being on the way.
    ///
    /// Deliberately tighter than <see cref="NearRadiusMeters"/>. A direct match
    /// asks the rider to walk to somebody's *planned* endpoint, which they
    /// opted into by choosing Nearby or Anywhere; a corridor match asks the
    /// driver to stop at a point they had not planned, and the driver never
    /// opted into anything. So this one is fixed rather than following the
    /// rider's toggle.
    /// </summary>
    public const int CorridorMeters = 2000;

    /// <summary>
    /// The most a corridor pickup and drop-off may add to the driver's run,
    /// as a share of the run itself.
    ///
    /// A share rather than a distance because the same 4 km detour is trivial on
    /// an intercity leg and absurd across town. <see cref="MinDetourMeters"/>
    /// keeps the short end sane: on a 3 km hop a pure fraction would refuse
    /// every joiner, so a small absolute allowance is always granted.
    ///
    /// Straight-line, like everything else in the MVP: it bounds the *ends*, not
    /// the driving. A real added-distance figure needs route geometry, and this
    /// is expressed against the route so it improves the day that arrives.
    /// </summary>
    public const double MaxDetourFraction = 0.25;

    public const int MinDetourMeters = 1500;

    /// <summary>The detour a run of <paramref name="routeKm"/> will tolerate, in km.</summary>
    public static double MaxDetourKm(double routeKm) =>
        Math.Max(MinDetourMeters / 1000.0, routeKm * MaxDetourFraction);

    // ── Ranking across tiers ──
    //
    // There is deliberately no tier weight here any more. A direct match cannot
    // be beaten by a corridor match, however convenient the corridor one looks,
    // and that used to be arranged by adding a constant large enough to swamp
    // every sort key — which held only while the keys stayed small, and two of
    // them did not. The band is now the first key the ranking orders on
    // (SearchService.Rank), so no magnitude of anything else can cross it.
}
