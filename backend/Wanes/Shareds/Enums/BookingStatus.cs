namespace Wanes.Shareds.Enums;

/// <summary>
/// One rider's seat, tracked by the trip's driver from the moment they reach
/// that rider to the moment they drop them off. The trip's own
/// <see cref="TripStatus"/> is derived from these — see
/// <c>Areas/Domain/Trips/TripStatusRules</c>.
/// </summary>
public enum BookingStatus
{
    Pending = 1,
    Confirmed = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5,

    /// <summary>Driver is at <em>this</em> rider's pickup point, waiting for them to board.</summary>
    Arrived = 6,

    /// <summary>Driver waited and the rider never boarded. Terminal, and not a cancellation.</summary>
    NoShow = 7,
}
