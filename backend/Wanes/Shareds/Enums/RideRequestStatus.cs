namespace Wanes.Shareds.Enums;

/// <summary>
/// Where a rider's demand stands. **Its own lifecycle, not a trip's** — that is
/// the whole of v2's change: demand that nobody is driving yet is a different
/// kind of thing from transportation that exists, and borrowing
/// <see cref="TripStatus"/> for it forced every downstream reader to ask "is
/// this real supply or a wish?" and get the answer right in search, in
/// availability, in reporting and in the console all at once.
/// </summary>
public enum RideRequestStatus
{
    /// <summary>
    /// Looking for a driver. Joinable by compatible riders, discoverable by
    /// drivers, and it stays this way until its own departure — giving the
    /// marketplace time to find supply is the entire point of writing one.
    /// </summary>
    Open = 1,

    /// <summary>
    /// A driver was selected and a trip was formed from it
    /// (<c>RideRequest.MatchedTripId</c>). Terminal: the request has done its
    /// job, and everything afterwards happens on the trip.
    /// </summary>
    Matched = 2,

    /// <summary>The last participant left, or an admin closed it.</summary>
    Cancelled = 3,

    /// <summary>Its departure came and went with nobody driving it.</summary>
    Expired = 4,
}

/// <summary>One rider's membership of a request.</summary>
public enum RideRequestParticipantStatus
{
    Active = 1,

    /// <summary>
    /// Gave their seats back. Kept rather than deleted so the request's history
    /// still says who asked for it — and so "the last one left" is a count over
    /// rows that were always there.
    /// </summary>
    Left = 2,
}

/// <summary>
/// A driver's answer to demand. Interest is a **signal**, not a trip: it means
/// "I am willing to serve this, subject to the platform's matching and
/// confirmation rules", and it carries everything an offer needs so that the day
/// riders choose between drivers, nothing in the domain has to change.
/// </summary>
public enum DriverInterestStatus
{
    Interested = 1,

    /// <summary>The driver took it back before anybody was selected.</summary>
    Withdrawn = 2,

    /// <summary>This is the driver the request was matched to.</summary>
    Selected = 3,

    /// <summary>Another driver was selected. Told, rather than left to discover it.</summary>
    Rejected = 4,

    /// <summary>The request closed without ever being matched.</summary>
    Expired = 5,
}
