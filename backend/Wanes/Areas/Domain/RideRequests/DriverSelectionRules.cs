using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Domain.RideRequests;

/// <summary>
/// Which interested driver serves a request, and when that is decided.
///
/// The rule v2 exists to avoid baking in is "first click wins". Not because it
/// is a bad answer — for a thin marketplace it is the right one, and it is what
/// ships — but because as an *architectural invariant* it forecloses everything
/// after it: competing offers, riders choosing, a driver's rating counting for
/// anything. So it is expressed as a **window of zero**
/// (<c>AppConfiguration.DriverSelectionWindowMinutes</c>) rather than as the
/// absence of a decision:
///
/// <list type="bullet">
/// <item><b>0</b> — the window is empty, the first interest is selected the
/// moment it arrives, and drivers experience exactly what the old claim did.</item>
/// <item><b>above 0</b> — interests accumulate for that long and <see cref="Best"/>
/// picks among them. Same inputs, same winner, every time.</item>
/// </list>
///
/// Nothing else in the domain knows which of the two is configured.
/// </summary>
public static class DriverSelectionRules
{
    /// <summary>
    /// First interest wins, immediately. The shipped default, and the behaviour
    /// every existing driver already understands.
    /// </summary>
    public const int ImmediateSelection = 0;

    /// <summary>
    /// The longest a pool may be held open for offers. Past this the riders are
    /// waiting on a decision the marketplace could have made for them, and a
    /// window that outlives the departure it is deciding about is not a window.
    /// </summary>
    public const int MaxSelectionWindowMinutes = 120;

    public static int WindowFor(int minutes) =>
        Math.Clamp(minutes, ImmediateSelection, MaxSelectionWindowMinutes);

    /// <summary>Whether selection happens the instant a driver expresses interest.</summary>
    public static bool IsImmediate(int windowMinutes) =>
        WindowFor(windowMinutes) == ImmediateSelection;

    /// <summary>
    /// When a request whose first interest arrived at <paramref name="firstInterestAt"/>
    /// should be decided.
    /// </summary>
    public static DateTime DecideAt(DateTime firstInterestAt, int windowMinutes) =>
        firstInterestAt.AddMinutes(WindowFor(windowMinutes));

    /// <summary>
    /// The winner among live interests, and the reason it is a total order.
    ///
    /// The priority is the doc's (§8.3): route and eligibility have already been
    /// enforced — an ineligible driver never gets an interest row — so what is
    /// left to weigh is fit, capacity, reputation and price. Every comparison
    /// ends in the same tie-break, <b>earliest interest then lowest id</b>, so
    /// two runs over the same rows cannot disagree. A selection that could
    /// answer differently on a retry is not a selection; it is a coin toss with
    /// a database behind it.
    ///
    /// <paramref name="drivers"/> supplies the rating and completion history;
    /// an interest whose driver is missing is ranked last rather than dropped,
    /// because dropping it could leave a request with no driver at all.
    /// </summary>
    public static DriverInterest? Best(
        IEnumerable<DriverInterest> interests,
        IReadOnlyDictionary<int, User> drivers,
        int seatsRequested)
    {
        return interests
            .Where(i => i.IsLive)
            .OrderByDescending(i => Fits(i, seatsRequested))
            .ThenByDescending(i => Rating(i, drivers))
            .ThenByDescending(i => Completed(i, drivers))
            .ThenBy(i => i.PricePerSeat)
            .ThenBy(i => i.CreationDate)
            .ThenBy(i => i.Id)
            .FirstOrDefault();
    }

    /// <summary>
    /// The car is big enough for everybody who asked. First, and as a hard
    /// sort key rather than a filter: a request that only under-sized cars
    /// answered still has to produce a driver or produce nothing, and ranking
    /// keeps that decision in one place.
    /// </summary>
    private static bool Fits(DriverInterest interest, int seatsRequested) =>
        interest.Vehicle == null || interest.Vehicle.SeatCapacity >= seatsRequested;

    private static double Rating(DriverInterest interest, IReadOnlyDictionary<int, User> drivers) =>
        drivers.TryGetValue(interest.DriverId, out var driver) ? driver.RatingAvg : 0;

    private static int Completed(DriverInterest interest, IReadOnlyDictionary<int, User> drivers) =>
        drivers.TryGetValue(interest.DriverId, out var driver) ? driver.TripsAsDriver : 0;

    /// <summary>
    /// What every other live interest becomes once one is selected.
    /// <see cref="DriverInterestStatus.Rejected"/> rather than silence: a driver
    /// who offered a car and heard nothing has no way to tell the difference
    /// between "somebody else got it" and "the app is broken".
    /// </summary>
    public const DriverInterestStatus LosingStatus = DriverInterestStatus.Rejected;
}
