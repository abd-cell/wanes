namespace Wanes.Shareds.Enums;

/// <summary>
/// Why a trip stopped waiting for a driver — the wording a driver's screen puts
/// on a card that is about to disappear.
///
/// Deliberately not <see cref="TripStatus"/>. The status says where the trip
/// stands; this says what happened to the *offer*, and the two do not line up:
/// a trip nobody took and a trip whose last rider left both land on
/// <see cref="TripStatus.Cancelled"/>, but "nobody took it" and "they withdrew
/// it" are different things to tell a driver who was looking at it. The three
/// call sites know which is which, so the frame carries it rather than
/// re-deriving it from a column that cannot tell them apart.
/// </summary>
public enum RiderTripClosedReason
{
    /// <summary>The last rider holding a seat left it.</summary>
    Cancelled = 1,

    /// <summary>Another driver got there first.</summary>
    Claimed = 2,

    /// <summary>Its departure came and went with nobody taking it.</summary>
    Expired = 3,
}
