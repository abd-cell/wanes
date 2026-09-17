using System.Globalization;
using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.Schedules;
using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Domain.Series;

/// <summary>
/// The rules of committing to a whole series, in one place.
///
/// A series is a promise about many days, so its cancellation rules are about
/// notice rather than about the one day:
/// <list type="bullet">
/// <item><b>Skip a day</b> with at least <see cref="DefaultSkipNoticeHours"/>
/// notice — free, up to <see cref="DefaultFreeSkipsPerWindow"/> in the
/// reliability window; the riders go back on the market. Shorter notice is a
/// late cancellation.</item>
/// <item><b>End the series</b> with <see cref="DefaultEndNoticeDays"/> notice —
/// free: the days inside the notice still run. Ending at once costs a point
/// for every day dropped inside the notice.</item>
/// </list>
/// Every number is a default for an admin setting.
/// </summary>
public static class SeriesRules
{
    public const int DefaultDecisionHours = 12;
    public const int DefaultSkipNoticeHours = 24;
    public const int DefaultFreeSkipsPerWindow = 4;
    public const int DefaultEndNoticeDays = 7;

    /// <summary>Saturday — the evening before the Sunday–Thursday week.</summary>
    public const int DefaultSummaryDay = (int)DayOfWeek.Saturday;

    public const int MaxDecisionHours = 7 * 24;
    public const int MaxSkipNoticeHours = 7 * 24;
    public const int MaxFreeSkips = 50;
    public const int MaxEndNoticeDays = 30;

    public const int EndShortNoticePoints = 1;

    /// <summary>
    /// How skipping one day of a series is recorded.
    ///
    /// - Nobody was aboard → free.
    /// - Underway, or inside the notice → late.
    /// - Enough notice, free skips left → a free skip.
    /// - Enough notice, free skips used up → an ordinary cancellation.
    /// </summary>
    public static ReliabilityEventKind ClassifySkip(
        DateTime now,
        DateTime departAt,
        TripStatus status,
        int ridersAffected,
        int skipNoticeHours,
        int freeSkipsUsed,
        int freeSkipsAllowed)
    {
        if (ridersAffected <= 0) return ReliabilityEventKind.FreeCancel;

        var underway = status is TripStatus.EnRoute or TripStatus.Arrived or TripStatus.Active;
        if (underway || IsShortNotice(now, departAt, skipNoticeHours)) return ReliabilityEventKind.LateCancel;

        return freeSkipsUsed < freeSkipsAllowed ? ReliabilityEventKind.SeriesSkip : ReliabilityEventKind.Cancel;
    }

    /// <summary>A day of a series given up with less notice than the setting asks.</summary>
    public static bool IsShortNotice(DateTime now, DateTime departAt, int skipNoticeHours) =>
        departAt - now < TimeSpan.FromHours(skipNoticeHours);

    /// <summary>The last day a series ended with notice still runs.</summary>
    public static DateOnly NoticeEnd(DateOnly today, int noticeDays) => today.AddDays(Math.Max(noticeDays, 0));

    /// <summary>A day dropped by ending at once, close enough to count.</summary>
    public static bool InsideEndNotice(DateOnly date, DateOnly today, int noticeDays) =>
        date <= NoticeEnd(today, noticeDays);

    /// <summary>
    /// The days a commitment may cover: a subset of the schedule's own. None
    /// stays None (every day the schedule runs). A subset that names none of the
    /// schedule's days would cover nothing, and is refused by returning null.
    /// </summary>
    public static WeekDays? NormaliseDays(WeekDays requested, TripSchedule schedule)
    {
        if (requested == WeekDays.None) return WeekDays.None;
        if (schedule.Recurrence != Recurrence.Weekly) return requested;
        var within = requested & schedule.DaysOfWeek;
        if (within == WeekDays.None) return null;
        return within == schedule.DaysOfWeek ? WeekDays.None : within;
    }

    /// <summary>
    /// The best of several drivers offering for one series, when the rider left
    /// it to the marketplace: the most reliable first, then the best rated,
    /// then the cheapest, then the first to offer.
    /// </summary>
    public static SeriesCommitment? Best(IEnumerable<SeriesCommitment> proposals, IReadOnlyDictionary<int, User> drivers) =>
        proposals
            .OrderByDescending(p => drivers.TryGetValue(p.DriverId, out var d)
                ? ReliabilityRules.CompletionRate(d.TripsAsDriver, d.DriverCancellations) ?? 1
                : 0)
            .ThenByDescending(p => drivers.TryGetValue(p.DriverId, out var d) ? d.RatingAvg : 0)
            .ThenBy(p => p.PricePerSeat ?? decimal.MaxValue)
            .ThenBy(p => p.Id)
            .FirstOrDefault();

    // ── Wording ──────────────────────────────────────────────────────────────

    private static readonly DayOfWeek[] Week =
    [
        DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday,
    ];

    private static readonly string[] ShortEn = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
    private static readonly string[] ShortAr = ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];
    private static readonly string[] MonthsAr =
        ["يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"];

    /// <summary>"Sun–Thu", "Every day", "Mon, Wed" — for notification text.</summary>
    public static (string En, string Ar) DaysLabel(TripSchedule schedule, WeekDays subset)
    {
        var days = subset != WeekDays.None ? subset
            : schedule.Recurrence == Recurrence.Weekly ? schedule.DaysOfWeek
            : WeekDays.None;

        if (days == WeekDays.None)
        {
            return schedule.Recurrence == Recurrence.Monthly && schedule.DayOfMonth is { } dom
                ? ($"Monthly on the {dom}", $"شهرياً يوم {dom}")
                : ("Every day", "كل يوم");
        }

        var on = Week.Where(d => days.HasFlag(RecurrenceRules.Flag(d))).Select(d => (int)d).ToList();
        if (on.Count == 7) return ("Every day", "كل يوم");

        // A run of consecutive days reads as a range.
        var consecutive = on.Count >= 3 && on.Zip(on.Skip(1)).All(p => p.Second == p.First + 1);
        if (consecutive)
            return ($"{ShortEn[on[0]]}–{ShortEn[on[^1]]}", $"{ShortAr[on[0]]}–{ShortAr[on[^1]]}");

        return (string.Join(", ", on.Select(i => ShortEn[i])), string.Join("، ", on.Select(i => ShortAr[i])));
    }

    /// <summary>"Tue 15 Sep" / "الثلاثاء 15 سبتمبر".</summary>
    public static (string En, string Ar) DateLabel(DateOnly date) =>
        ($"{ShortEn[(int)date.DayOfWeek]} {date.Day} {date.ToString("MMM", CultureInfo.InvariantCulture)}",
         $"{ShortAr[(int)date.DayOfWeek]} {date.Day} {MonthsAr[date.Month - 1]}");
}
