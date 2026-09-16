namespace Wanes.Areas.Domain.RiderTrips;

/// <summary>
/// What a rider may ask for when they post a trip of their own.
///
/// The one rule with teeth is the lead time. A rider posting for four seats is
/// not ordering a taxi — they are asking a driver to *gather* four people and
/// then run the leg, which costs roughly one leg-time per seat. A departure that
/// leaves no room for that produces a posting no driver can physically serve,
/// and an unservable posting is worse than none: it sits on the board, collects
/// a claim, and then fails somebody in the street.
///
/// So the earliest departure a posting may name scales with the seats it asks
/// for. One seat costs a few minutes; four costs hours, deliberately.
/// </summary>
public static class RiderTripRules
{
    /// <summary>
    /// The speed the duration estimate assumes, when nobody has said otherwise.
    ///
    /// The live value is <c>AppConfiguration.AverageSpeedKmh</c>: what counts as
    /// normal progress is a city question — Amman in the afternoon is not a
    /// motorway — and the same estimate drives what the rider is told about
    /// arrival and what a claiming driver sees as a suggested price. This is
    /// what a fresh install starts with.
    /// </summary>
    public const double DefaultAverageSpeedKmh = 35;

    /// <summary>
    /// Bounds on the admin-set speed. Zero or negative would divide the estimate
    /// into infinity, and beyond the ceiling the lead-time rule stops protecting
    /// anything.
    /// </summary>
    public const double MinAverageSpeedKmh = 5;

    public const double MaxAverageSpeedKmh = 120;

    public static double SpeedFor(double kmh) =>
        double.IsFinite(kmh) ? Math.Clamp(kmh, MinAverageSpeedKmh, MaxAverageSpeedKmh) : DefaultAverageSpeedKmh;

    /// <summary>
    /// The floor under the computed lead. A 300 m hop estimates at under a
    /// minute, and "leaving in forty seconds" is not a posting a driver can
    /// reach — it is the same mistake as stamping a departure of <c>now</c>.
    /// </summary>
    public static readonly TimeSpan MinLead = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The ceiling. Eight seats over an intercity route multiplies out to most
    /// of a day, which stops describing a gathering run and starts refusing
    /// trips people would happily drive.
    /// </summary>
    public static readonly TimeSpan MaxLead = TimeSpan.FromHours(6);

    /// <summary>
    /// The most seats one posting may ask for, joiners included.
    ///
    /// A posting is claimed by *one* driver in *one* car, so past the largest
    /// vehicle the platform expects to see there is nobody who could take it.
    /// Growing beyond this is refused rather than trimmed: a rider who is told
    /// "this pool is full, post your own" can act on it, where a silently
    /// halved request fails at the kerb.
    /// </summary>
    public const int MaxSeats = 8;

    /// <summary>
    /// How far ahead of departure a scheduled posting starts pushing to drivers.
    ///
    /// It is on the board from the moment it is written — any driver can find and
    /// claim it — but a posting for Thursday must not wake every driver in town
    /// on Monday. An hour is about when a driver can still rearrange their
    /// evening around it.
    /// </summary>
    public static readonly TimeSpan NotifyLead = TimeSpan.FromHours(1);

    /// <summary>
    /// How long the leg itself should take, at <paramref name="averageSpeedKmh"/>.
    /// Zero-length and non-finite distances answer zero rather than throwing —
    /// the caller's floor is what makes the result usable.
    /// </summary>
    public static TimeSpan EstimatedDuration(double km, double averageSpeedKmh)
    {
        var distance = double.IsFinite(km) && km > 0 ? km : 0;
        var hours = distance / SpeedFor(averageSpeedKmh);
        return TimeSpan.FromHours(hours);
    }

    /// <summary>
    /// The gathering allowance for <paramref name="seats"/> seats over a
    /// <paramref name="km"/> leg: one leg-time per seat, clamped to
    /// <see cref="MinLead"/>..<see cref="MaxLead"/>.
    /// </summary>
    public static TimeSpan MinimumLead(double km, int seats, double averageSpeedKmh)
    {
        var perSeat = EstimatedDuration(km, averageSpeedKmh);
        var raw = perSeat * Math.Max(seats, 1);
        if (raw < MinLead) return MinLead;
        return raw > MaxLead ? MaxLead : raw;
    }

    /// <summary>
    /// The earliest departure a posting for these seats over this distance may
    /// name. Anything sooner is <c>ErrorCode.DepartureTooSoon</c>.
    /// </summary>
    public static DateTime EarliestDeparture(DateTime now, double km, int seats, double averageSpeedKmh) =>
        now + MinimumLead(km, seats, averageSpeedKmh);

    /// <summary>
    /// Whether a posting departing at <paramref name="departAt"/> should be
    /// pushed to nearby drivers now, rather than left to be found on the board.
    /// </summary>
    public static bool ShouldNotifyNow(DateTime departAt, DateTime now) =>
        departAt - now <= NotifyLead;
}
