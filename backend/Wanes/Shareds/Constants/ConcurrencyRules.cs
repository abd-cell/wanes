namespace Wanes.Shareds.Constants;

/// <summary>
/// How the platform's two genuine races are settled.
///
/// Both are last-seat problems: two riders taking the final seat on a trip, and
/// two drivers claiming the same rider-posted trip. Neither is safe as a read-check-write
/// inside a transaction — SQL Server reads at READ COMMITTED, so both readers
/// see the same row and neither blocks the other. The rows therefore carry a
/// row version (<c>Trip.RowVersion</c>, <c>RiderTrip.RowVersion</c>), which
/// makes every UPDATE conditional on the value that was read: the loser changes
/// no rows and is told, rather than silently overwriting the winner.
/// </summary>
public static class ConcurrencyRules
{
    /// <summary>
    /// How many times an operation re-reads and tries again after losing.
    ///
    /// A loss is not automatically a failure. Two riders booking a four-seat
    /// trip at the same instant both deserve a seat — the loser only needs to
    /// look again. A retry is only pointless once the fresh read says no (the
    /// trip is Full, the hail is taken), and that answer comes out of the normal
    /// checks rather than from here.
    ///
    /// Three because contention on one trip row is measured in riders, not
    /// threads: with the third read still losing, something is wrong beyond a
    /// coincidence and returning <c>Conflict</c> beats looping.
    /// </summary>
    public const int MaxAttempts = 3;
}
