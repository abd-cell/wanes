using Wanes.Shareds.Enums;

namespace Wanes.Areas.Domain.Bookings;

/// <summary>
/// What a driver may do to one rider's seat, and which seats still count as
/// held. One table, because three services and two output shapes ask the same
/// two questions and must never disagree.
/// </summary>
public static class BookingStatusRules
{
    /// <summary>
    /// The seat is still held: the rider has neither given it back nor finished
    /// the ride. This is what gates the two parties' phone numbers and the
    /// driver's live position, so a status added without a line here silently
    /// takes those away.
    /// </summary>
    public static bool IsLive(BookingStatus status) => status is BookingStatus.Pending
        or BookingStatus.Confirmed
        or BookingStatus.Arrived
        or BookingStatus.InProgress;

    /// <summary>Nothing more will happen to this seat.</summary>
    public static bool IsTerminal(BookingStatus status) => !IsLive(status);

    /// <summary>
    /// The seat is held but nobody is committed: the trip has not reached the
    /// seats its driver asked for. It counts against the trip's capacity — a
    /// held seat is not for sale twice — and it is what
    /// <see cref="Wanes.Areas.Domain.Trips.TripConfirmationRules.HeldSeats"/>
    /// weighs against the threshold.
    /// </summary>
    public static bool IsPending(BookingStatus status) => status == BookingStatus.Pending;

    /// <summary>
    /// Which move the trip's driver may make on a seat from where it stands.
    ///
    /// The rider's tracking rail reads straight off these, so an out-of-order
    /// jump would show them a stage that never happened — refuse it here rather
    /// than let the rail lie. A rider is picked up only after being reached
    /// (or straight from Confirmed, for a driver who never taps Arrived), and
    /// dropped off only once aboard.
    ///
    /// <see cref="BookingStatus.Pending"/> is the seat the driver cannot touch.
    /// Nobody has committed to it — the trip has not met its threshold — so
    /// carrying that rider would settle a question that is still open. It
    /// resolves before departure either way: the threshold is met, the driver
    /// runs the trip anyway, or the trip is called off.
    /// </summary>
    public static bool CanDriverSet(BookingStatus from, BookingStatus to) => to switch
    {
        BookingStatus.Arrived => from is BookingStatus.Confirmed,
        BookingStatus.InProgress => from is BookingStatus.Confirmed or BookingStatus.Arrived,
        BookingStatus.Completed => from is BookingStatus.InProgress,
        BookingStatus.NoShow => from is BookingStatus.Confirmed or BookingStatus.Arrived,
        _ => false,
    };

}
