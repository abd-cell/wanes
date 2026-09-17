using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Domain.Marketplace;

/// <summary>
/// What a cancellation costs, and when a record of them stops a driver taking
/// instant work.
///
/// The marketplace is shared rides planned ahead: a driver who accepts a pool
/// and then walks away strands several people at once, usually with too little
/// time to find another car. With no payments there is no fee to charge, so the
/// lever is the record — visible on the driver's card, weighed when riders'
/// requests are awarded, and, past a threshold, a pause on instant requests.
///
/// Every number here is a default for an admin setting.
/// </summary>
public static class ReliabilityRules
{
    /// <summary>Minutes after accepting in which a driver may back out for free — the mis-tap.</summary>
    public const int DefaultFreeCancelGraceMinutes = 3;

    /// <summary>A cancellation closer than this to departure is late, and weighs double.</summary>
    public const int DefaultLateCancelLeadMinutes = 120;

    public const int DefaultWarnPoints = 3;
    public const int DefaultSuspendPoints = 5;
    public const int DefaultWindowDays = 30;
    public const int DefaultSuspensionDays = 7;

    public const int MaxGraceMinutes = 60;
    public const int MaxLateLeadMinutes = 24 * 60;
    public const int MaxPoints = 100;
    public const int MaxWindowDays = 365;
    public const int MaxSuspensionDays = 90;

    public const int CancelPoints = 1;
    public const int LateCancelPoints = 2;
    public const int RiderLateCancelPoints = 1;
    public const int RiderNoShowPoints = 1;

    /// <summary>
    /// How a driver's cancellation is recorded.
    ///
    /// - Nobody depended on it (no live seat) → free.
    /// - Inside the grace period of accepting, and not close to departure → free.
    /// - Close to departure, or after setting off → late.
    /// - Otherwise → counted.
    /// </summary>
    public static ReliabilityEventKind ClassifyDriverCancel(
        DateTime now,
        DateTime acceptedAt,
        DateTime departAt,
        TripStatus status,
        int ridersAffected,
        int graceMinutes,
        int lateLeadMinutes)
    {
        if (ridersAffected <= 0) return ReliabilityEventKind.FreeCancel;

        var underway = status is TripStatus.EnRoute or TripStatus.Arrived or TripStatus.Active;
        var close = departAt - now <= TimeSpan.FromMinutes(lateLeadMinutes);
        if (underway || close) return ReliabilityEventKind.LateCancel;

        var withinGrace = now - acceptedAt <= TimeSpan.FromMinutes(graceMinutes);
        return withinGrace ? ReliabilityEventKind.FreeCancel : ReliabilityEventKind.Cancel;
    }

    public static int PointsFor(ReliabilityEventKind kind) => kind switch
    {
        ReliabilityEventKind.Cancel => CancelPoints,
        ReliabilityEventKind.LateCancel => LateCancelPoints,
        ReliabilityEventKind.RiderLateCancel => RiderLateCancelPoints,
        ReliabilityEventKind.RiderNoShow => RiderNoShowPoints,
        _ => 0,
    };

    /// <summary>Whether the entry counts as a cancellation on the driver's completion rate.</summary>
    public static bool CountsAgainstCompletion(ReliabilityEventKind kind) =>
        kind is ReliabilityEventKind.Cancel or ReliabilityEventKind.LateCancel;

    /// <summary>
    /// Reasons that may well not be the driver's fault. Still recorded, still
    /// counted until somebody looks — but flagged so an admin does look.
    /// </summary>
    public static bool NeedsReview(CancelReason? reason) =>
        reason is CancelReason.VehicleProblem or CancelReason.SafetyConcern or CancelReason.Emergency;

    /// <summary>
    /// Once riders depend on a trip, the driver has to say why they are
    /// leaving it. A trip nobody is on can be dropped without a word.
    /// </summary>
    public static bool ReasonRequired(int ridersAffected) => ridersAffected > 0;

    /// <summary>A rider giving their seat back this close to departure is recorded.</summary>
    public static bool IsLateRiderCancel(DateTime now, DateTime departAt, int lateLeadMinutes) =>
        departAt - now <= TimeSpan.FromMinutes(lateLeadMinutes);

    /// <summary>
    /// Completed trips over completed plus counted cancellations, 0–1. Null
    /// while there is nothing to go on — a new driver is not a bad driver.
    /// </summary>
    public static double? CompletionRate(int completed, int cancellations)
    {
        var total = completed + cancellations;
        return total <= 0 ? null : Math.Round((double)completed / total, 3);
    }

    /// <summary>The driver may not take instant requests until this passes.</summary>
    public static bool IsSuspended(User user, DateTime now) =>
        user.SuspendedUntil is { } until && until > now;

    /// <summary>What a record of <paramref name="points"/> in the window means now.</summary>
    public static (bool Warn, bool Suspend) Standing(int points, int warnPoints, int suspendPoints) =>
        (points >= warnPoints, points >= suspendPoints);
}

/// <summary>
/// The four digits a rider reads out at the kerb. On a shared ride several
/// people wait for several cars; the code is how each side knows it has the
/// right one.
/// </summary>
public static class BoardingCodes
{
    public const int Length = 4;

    public static string New() =>
        System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 10_000).ToString("D4");

    public static bool Matches(string? expected, string? given) =>
        expected == null
        || string.Equals(expected, given?.Trim(), StringComparison.Ordinal);

    /// <summary>An unguessable handle for a read-only trip link.</summary>
    public static string NewShareToken() => Guid.NewGuid().ToString("N");
}
