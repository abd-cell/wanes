using Wanes.Areas.Domain.Bookings;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Domain.Trips;

/// <summary>
/// A driver's "only if it is worth it" condition, and the clock that forces an
/// answer.
///
/// <c>Trip.MinSeatsToConfirm</c> says how many seats must be held before anybody
/// is committed, and whether the trip has them is derived from its bookings.
/// **Having once had them is not.** <c>Trip.ConfirmedAt</c> is stamped in the
/// commit that confirms the seats and is never cleared, because a confirmation
/// derived from the live seat count oscillates: a rider cancelling would
/// un-promise a ride to everybody else on the trip. The driver still never
/// toggles anything — reaching the threshold, or answering the prompt, is what
/// writes it.
///
/// The clock exists because a threshold that is never met has to resolve into an
/// answer riders can act on. Two moments:
///
/// <list type="bullet">
/// <item><b>the prompt</b>, <see cref="DecisionLead"/> before the cutoff — the
/// driver is asked to run with what they have, or call it off;</item>
/// <item><b>the cutoff</b>, <see cref="Cutoff"/> before departure — unanswered,
/// the trip cancels and the riders are told.</item>
/// </list>
///
/// Silence cancels rather than runs. The driver said they needed N seats, and
/// leaving riders holding unconfirmed seats until departure would strand them
/// with no time to find another ride — which is also why the cutoff is well
/// before departure and not departure itself.
/// </summary>
public static class TripConfirmationRules
{
    /// <summary>No condition: the first seat commits, as an ordinary trip does.</summary>
    public const int NoThreshold = 1;

    /// <summary>
    /// What a driver's threshold starts at when they express no preference.
    ///
    /// Three, and it is **platform policy rather than a per-driver invention**:
    /// what makes a run worth driving is a fact about the city and the route, not
    /// something each driver should have to rediscover on a form. The live value
    /// is <c>AppConfiguration.MinimumPassengersDefault</c>; a driver may still
    /// lower it to <see cref="NoThreshold"/> or raise it to the seats they offer.
    ///
    /// It seeds the field, it does not govern it: once written the trip's own
    /// number is the only one anything reads, so changing the default never
    /// re-decides a trip somebody already has seats on.
    /// </summary>
    public const int DefaultMinimumPassengers = 3;

    /// <summary>
    /// Bounds on the admin-set default. Above this, a fresh driver's first trip
    /// would need more passengers than an ordinary car can seat and would never
    /// confirm.
    /// </summary>
    public const int MaxMinimumPassengers = 8;

    /// <summary>The seeded threshold, clamped.</summary>
    public static int DefaultThreshold(int configured) =>
        Math.Clamp(configured <= 0 ? DefaultMinimumPassengers : configured,
            NoThreshold, MaxMinimumPassengers);

    /// <summary>
    /// How long before departure the driver's decision is due, by default. The
    /// live value is <c>AppConfiguration.ConfirmCutoffMinutes</c> — how much
    /// warning a stood-down rider needs is a local question.
    /// </summary>
    public const int DefaultCutoffMinutes = 60;

    /// <summary>
    /// How long before the cutoff the driver is prompted, by default. The live
    /// value is <c>AppConfiguration.ConfirmDecisionLeadMinutes</c>.
    /// </summary>
    public const int DefaultDecisionLeadMinutes = 15;

    /// <summary>
    /// Bounds on both windows. A cutoff under five minutes gives a stood-down
    /// rider no time at all, and past half a day it cancels trips that were
    /// still filling.
    /// </summary>
    public const int MinCutoffMinutes = 5;

    public const int MaxCutoffMinutes = 720;

    public const int MinDecisionLeadMinutes = 1;

    public const int MaxDecisionLeadMinutes = 240;

    public static TimeSpan Cutoff(int minutes) =>
        TimeSpan.FromMinutes(Math.Clamp(minutes, MinCutoffMinutes, MaxCutoffMinutes));

    public static TimeSpan DecisionLead(int minutes) =>
        TimeSpan.FromMinutes(Math.Clamp(minutes, MinDecisionLeadMinutes, MaxDecisionLeadMinutes));

    /// <summary>A threshold within what the trip can actually seat.</summary>
    public static int ThresholdFor(int minSeats, int seatsTotal) =>
        Math.Clamp(minSeats <= 0 ? NoThreshold : minSeats, NoThreshold, Math.Max(seatsTotal, NoThreshold));

    /// <summary>
    /// The threshold for a trip whose driver may have said nothing.
    ///
    /// Silence means the marketplace's default, not "no condition": a driver who
    /// never touched the field has not decided that one passenger is worth the
    /// run, they have decided nothing. Anything they *did* say is theirs and is
    /// only clamped to the seats on offer.
    /// </summary>
    public static int SeededThresholdFor(int minSeats, int seatsTotal, int configuredDefault) =>
        ThresholdFor(minSeats > 0 ? minSeats : DefaultThreshold(configuredDefault), seatsTotal);

    /// <summary>
    /// Seats standing behind a trip: everything still held, committed or not.
    ///
    /// Pending seats count. The threshold asks "will this run be worth making",
    /// and a rider who has taken a seat and not yet answered the price is as
    /// much a passenger-in-waiting as one who has — excluding them would leave a
    /// trip permanently one seat short of confirming itself.
    /// </summary>
    public static int HeldSeats(IEnumerable<Booking> bookings) =>
        bookings.Where(b => BookingStatusRules.IsLive(b.Status)).Sum(b => b.Seats);

    /// <summary>Whether the trip's seats meet its own threshold.</summary>
    public static bool IsMet(int minSeatsToConfirm, int heldSeats) =>
        heldSeats >= Math.Max(minSeatsToConfirm, NoThreshold);

    /// <summary>
    /// The trip is still gathering: it has a threshold, nothing has confirmed it
    /// yet, and it is in a state where seats can still arrive.
    ///
    /// <paramref name="confirmedAt"/> is what makes this stop being a function of
    /// the current seat count. Once a trip has confirmed it stays confirmed, so a
    /// rider cancelling back below the threshold frees a seat and changes nothing
    /// else — it does not un-tell the riders who were already promised a ride.
    /// The falling seat count is a signal to the driver, not a downgrade.
    /// </summary>
    public static bool IsGathering(
        TripStatus status,
        int minSeatsToConfirm,
        int heldSeats,
        DateTime? confirmedAt)
    {
        if (confirmedAt != null) return false;
        if (!TripStatusRules.IsOpenForSeats(status)) return false;
        return minSeatsToConfirm > NoThreshold && !IsMet(minSeatsToConfirm, heldSeats);
    }

    /// <summary>When the driver has to have answered by.</summary>
    public static DateTime DeadlineFor(DateTime departAt, int cutoffMinutes) =>
        departAt - Cutoff(cutoffMinutes);

    /// <summary>When the driver should be asked.</summary>
    public static DateTime PromptAtFor(DateTime departAt, int cutoffMinutes, int decisionLeadMinutes) =>
        DeadlineFor(departAt, cutoffMinutes) - DecisionLead(decisionLeadMinutes);
}
