namespace Wanes.Shareds.Enums;

public enum TripStatus
{
    Posted = 1,
    Full = 2,
    Active = 3,
    Completed = 4,
    Cancelled = 5,

    /// <summary>Driver is at the pickup point, waiting for the rider to board.</summary>
    Arrived = 6,

    /// <summary>
    /// The driver has set off and is on their way to the first pickup.
    ///
    /// Appended rather than slotted into the running order: the numbers cross
    /// all three stacks, so inserting one would silently re-label every stored
    /// row. Read the order from <c>TripService.CanTransition</c>, not from the
    /// values.
    ///
    /// This is the one status a driver sets outright rather than one derived
    /// from the seats — no rider's booking has moved yet, which is exactly what
    /// distinguishes it from <see cref="Arrived"/>. It is also where a trip
    /// stops being searchable: while there was no such state, a trip stayed
    /// discoverable all the way to the first kerb and then dropped out, which is
    /// backwards.
    /// </summary>
    EnRoute = 7,
}
