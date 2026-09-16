namespace Wanes.Shareds.Enums;

/// <summary>
/// How often a schedule produces a trip.
///
/// Three values on purpose. A schedule is a *generator* — a worker turns it
/// into ordinary trips over a rolling horizon — so anything expressible here
/// has to be answerable for a given date without asking the user again. Daily,
/// a set of weekdays, and a day of the month cover how people actually commute;
/// a cron-shaped field would let someone write a recurrence nobody can read
/// back on a card.
/// </summary>
public enum Recurrence
{
    /// <summary>Every day between the schedule's start and end.</summary>
    Daily = 1,

    /// <summary>On the days named by <c>TripSchedule.DaysOfWeek</c>.</summary>
    Weekly = 2,

    /// <summary>
    /// On <c>TripSchedule.DayOfMonth</c>, falling back to the last day of a
    /// shorter month — see <c>Areas/Domain/Schedules/RecurrenceRules</c>.
    /// </summary>
    Monthly = 3,
}

/// <summary>
/// The days a <see cref="Recurrence.Weekly"/> schedule runs on.
///
/// Flags rather than a child table: a weekly schedule's days are read and
/// written as one value on one screen, and a seven-row join for "Sun + Tue"
/// buys nothing. The bit order follows <see cref="DayOfWeek"/> so the
/// conversion is a shift and not a lookup.
/// </summary>
[Flags]
public enum WeekDays
{
    None = 0,
    Sunday = 1 << 0,
    Monday = 1 << 1,
    Tuesday = 1 << 2,
    Wednesday = 1 << 3,
    Thursday = 1 << 4,
    Friday = 1 << 5,
    Saturday = 1 << 6,
    All = Sunday | Monday | Tuesday | Wednesday | Thursday | Friday | Saturday,
}
