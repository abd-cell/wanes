using Wanes.Shareds.Enums;

namespace Wanes.Areas.Domain.Schedules;

/// <summary>
/// How a schedule unrolls into dates, and how a local wall-clock time becomes
/// the instant a trip departs.
///
/// A schedule never matches anything: a worker turns it into ordinary trips and
/// rider-posted trips over a rolling horizon, and search, booking, claiming and
/// the driver's availability rules never learn that recurrence exists. So the
/// whole of recurrence lives here, in one pure function of a date range — which
/// is also what makes it testable without a database.
///
/// The two calendar edges are *decided*, not discovered:
///
/// <list type="bullet">
/// <item>a monthly schedule on the 31st runs on the <b>last day</b> of shorter
/// months, because the alternative is a commute that silently skips
/// February;</item>
/// <item>a time of day that a daylight-saving shift deletes moves <b>forward</b>
/// to the next real local time, because the alternative is a trip that never
/// gets generated and nobody notices until nobody arrives.</item>
/// </list>
/// </summary>
public static class RecurrenceRules
{
    /// <summary>
    /// How far ahead occurrences are materialised.
    ///
    /// Long enough that riders can find next week's commute and plan around it,
    /// short enough that a schedule someone edits or pauses has not already
    /// written a month of rows that have to be cleaned up. Two weeks is also
    /// comfortably more than any client shows on one screen.
    /// </summary>
    public const int HorizonDays = 14;

    /// <summary>
    /// A ceiling on one materialisation pass, as a stop against a schedule whose
    /// window somehow spans years — a bug should cost one short pass, not a
    /// table full of trips.
    /// </summary>
    public const int MaxOccurrencesPerPass = 100;

    /// <summary>The <see cref="WeekDays"/> bit for a <see cref="DayOfWeek"/>.</summary>
    public static WeekDays Flag(DayOfWeek day) => (WeekDays)(1 << (int)day);

    /// <summary>Whether a weekly day-set includes this date's weekday.</summary>
    public static bool Includes(WeekDays days, DateOnly date) => days.HasFlag(Flag(date.DayOfWeek));

    /// <summary>
    /// The day of the month a schedule on <paramref name="dayOfMonth"/> runs on
    /// in the month of <paramref name="date"/> — clamped to the month's length,
    /// so the 31st becomes the 30th, the 29th or the 28th rather than nothing.
    /// </summary>
    public static int EffectiveDayOfMonth(int dayOfMonth, DateOnly date)
    {
        var length = DateTime.DaysInMonth(date.Year, date.Month);
        return Math.Clamp(dayOfMonth, 1, length);
    }

    /// <summary>Whether the recurrence produces an occurrence on this date.</summary>
    public static bool Runs(Recurrence recurrence, WeekDays days, int? dayOfMonth, DateOnly date) =>
        recurrence switch
        {
            Recurrence.Daily => true,
            Recurrence.Weekly => Includes(days, date),
            Recurrence.Monthly => dayOfMonth != null
                                  && date.Day == EffectiveDayOfMonth(dayOfMonth.Value, date),
            _ => false,
        };

    /// <summary>
    /// Every date the recurrence produces in <paramref name="from"/>..<paramref name="to"/>
    /// inclusive, in order. An empty range or a recurrence that names no days
    /// yields nothing — the caller reports
    /// <c>ErrorCode.ScheduleHasNoOccurrences</c> rather than storing a schedule
    /// that can never fire.
    /// </summary>
    public static IEnumerable<DateOnly> Occurrences(
        Recurrence recurrence, WeekDays days, int? dayOfMonth, DateOnly from, DateOnly to)
    {
        var produced = 0;
        for (var date = from; date <= to && produced < MaxOccurrencesPerPass; date = date.AddDays(1))
        {
            if (!Runs(recurrence, days, dayOfMonth, date)) continue;
            produced++;
            yield return date;
        }
    }

    /// <summary>
    /// The instant a trip on <paramref name="date"/> at <paramref name="timeOfDay"/>
    /// departs, in UTC.
    ///
    /// The schedule stores a wall-clock time because that is what people mean —
    /// "the seven o'clock" stays the seven o'clock across a clock change. Which
    /// makes the conversion the interesting part: a shift-forward deletes local
    /// times outright, so an invalid one is walked forward a minute at a time
    /// until it exists, and an ambiguous one (the repeated hour) resolves to the
    /// first pass, which is what a rider reading "07:00" will turn up for.
    /// </summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly timeOfDay, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(timeOfDay, DateTimeKind.Unspecified);

        // Walk out of a deleted hour. Bounded because a zone that reported every
        // minute invalid would otherwise spin — no real zone does, and a wrong
        // answer here is better than a hung worker.
        for (var attempt = 0; attempt < 180 && zone.IsInvalidTime(local); attempt++)
            local = local.AddMinutes(1);

        return TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
    }

    /// <summary>
    /// The zone a schedule's times are read in, tolerating an id this machine
    /// does not know.
    ///
    /// Falls back to UTC rather than throwing: a schedule stored on a host with a
    /// full time-zone database must not stop generating trips because a
    /// container somewhere has a trimmed one. The trips it makes would be off by
    /// an offset, which is visible and fixable; a worker that dies is neither.
    /// </summary>
    public static TimeZoneInfo ZoneFor(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Phones report a zone as an offset ("+03", "GMT+03:00", "UTC+3")
            // far more often than as an id. Read as a fixed offset it is right
            // wherever the clocks do not change — which beats UTC everywhere.
            return OffsetZone(id) ?? TimeZoneInfo.Utc;
        }
    }

    private static readonly System.Text.RegularExpressions.Regex OffsetPattern =
        new(@"^(?:UTC|GMT)?\s*([+-])(\d{1,2})(?::?(\d{2}))?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>A fixed-offset zone for "+03", "GMT+03:00", "UTC-5"; null for anything else.</summary>
    public static TimeZoneInfo? OffsetZone(string id)
    {
        var match = OffsetPattern.Match(id.Trim());
        if (!match.Success) return null;

        var hours = int.Parse(match.Groups[2].Value);
        var minutes = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
        if (hours > 14 || minutes > 59) return null;

        var offset = new TimeSpan(hours, minutes, 0);
        if (match.Groups[1].Value == "-") offset = offset.Negate();
        var name = $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset:hh\\:mm}";
        return TimeZoneInfo.CreateCustomTimeZone(name, offset, name, name);
    }
}
