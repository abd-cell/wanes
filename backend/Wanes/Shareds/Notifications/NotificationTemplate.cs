namespace Wanes.Shareds.Notifications;

/// <summary>
/// One localisable notification message.
///
/// Separate from <see cref="Wanes.Shareds.Enums.NotificationType"/> because a
/// type can carry more than one message: a confirmed booking says one thing to
/// the rider and another to the driver, but both are still
/// <c>NotificationType.BookingConfirmed</c> to the inbox. The type is what the
/// app filters and styles on; the template is what produces the words.
///
/// Call sites pass one of these plus arguments instead of a formatted string,
/// so <see cref="NotificationTexts"/> can render the same message in English and
/// Arabic for whoever is receiving it.
/// </summary>
public enum NotificationTemplate
{
    RiderTripNearbyDriver = 1,
    BookingConfirmedRider = 2,
    BookingConfirmedDriver = 3,
    BookingCancelledDriver = 4,
    TripCancelledRider = 5,
    TripStartedRider = 6,
    TripCompletedRider = 7,
    TripMatchedRider = 8,
    RiderTripClaimedRider = 9,
    DriverVerified = 10,
    DriverRejected = 11,
    RatingReceived = 12,
    DriverArrivedRider = 13,

    /// <summary>The driver waited at the pickup and marked the rider a no-show.</summary>
    BookingNoShowRider = 14,

    /// <summary>The support desk answered the user's complaint or suggestion.</summary>
    FeedbackReplied = 15,

    /// <summary>
    /// A declined driver application where the reviewer wrote down why. Its own
    /// template rather than a placeholder on <see cref="DriverRejected"/>: a
    /// note is optional, and a body reading "Reason: " with nothing after it is
    /// worse than the generic wording.
    /// </summary>
    DriverRejectedWithReason = 16,

    /// <summary>The trip reached the seats its driver asked for — every held seat is committed.</summary>
    TripConfirmedRider = 17,

    /// <summary>
    /// The trip was called off for want of riders. Its own template rather than
    /// <see cref="TripCancelledRider"/>: "the driver cancelled" reads as a
    /// choice made about you, and this one was made about the empty seats.
    /// </summary>
    TripLowSeatsCancelledRider = 18,

    /// <summary>
    /// The driver's run-or-cancel question on a trip short of its threshold.
    /// The one notification in the set that asks for an answer rather than
    /// reporting one, which is why it is sent once and not on every sweep.
    /// </summary>
    TripConfirmDecisionDriver = 19,

    /// <summary>
    /// Another rider joined a request this rider is on. Worth a notification
    /// because it is the one thing that visibly moves while they wait: a pool of
    /// three is a much better proposition to a driver than one seat.
    /// </summary>
    RideRequestJoinedRider = 20,

    /// <summary>
    /// A driver offered to serve the request. Sent only where offers accumulate
    /// — with immediate selection the rider hears about the trip instead, and
    /// two notifications a second apart would say the same thing twice.
    /// </summary>
    RideRequestInterestRider = 21,

    /// <summary>
    /// A driver was selected and the ride now exists. Carries the trip's id,
    /// which is the moment the rider's screens stop following the request.
    /// </summary>
    RideRequestMatchedRider = 22,

    /// <summary>
    /// Somebody else was selected. Sent rather than left to silence: a driver
    /// who offered a car and heard nothing cannot tell "they picked another
    /// driver" from "the app is broken".
    /// </summary>
    RideRequestNotSelectedDriver = 23,

    /// <summary>A driver took the request on condition it reaches their minimum — the riders are gathering.</summary>
    RideRequestMatchedGatheringRider = 24,

    /// <summary>The driver cancelled, and the riders were put back on the market in a new request.</summary>
    TripCancelledReopenedRider = 25,

    /// <summary>A route alert, or a watched request, reached the driver's seat count.</summary>
    DemandAlertMatchedDriver = 26,

    /// <summary>The driver's cancellations are close to pausing instant requests.</summary>
    ReliabilityWarningDriver = 27,

    /// <summary>Instant requests are paused for the driver until a date.</summary>
    ReliabilitySuspendedDriver = 28,

    /// <summary>An SOS or safety report for the admin team.</summary>
    SafetyIncidentAdmin = 29,

    /// <summary>A rider picked this driver's offer.</summary>
    OfferChosenDriver = 30,

    // ── Series (recurring commitments) ──
    //
    // Every day-level message names the day. "Your recurring trip" tells a
    // rider nothing they can act on; "Tuesday 15 Sep" does.

    /// <summary>A driver offered to drive the rider's whole series.</summary>
    SeriesOfferRider = 31,

    /// <summary>The driver's series offer was accepted — by the rider or by the clock.</summary>
    SeriesAcceptedDriver = 32,

    /// <summary>The rider went with another driver for the series.</summary>
    SeriesNotSelectedDriver = 33,

    /// <summary>The rider's series has a driver now.</summary>
    SeriesStartedRider = 34,

    /// <summary>The series driver could not be given one day; it is on the board for anyone.</summary>
    SeriesDayOpenRider = 35,

    /// <summary>One day was not added to the driver's series — they are busy then.</summary>
    SeriesDayMissedDriver = 36,

    /// <summary>The driver skipped one day of the series.</summary>
    SeriesDaySkippedRider = 37,

    /// <summary>The other side ended the series.</summary>
    SeriesEnded = 38,

    /// <summary>The week ahead on a series.</summary>
    SeriesWeeklySummary = 39,

    /// <summary>A rider booked every day of the driver's recurring trip.</summary>
    SeriesRiderJoinedDriver = 40,

    /// <summary>A day of a rider's series booking could not be booked (full, or they are busy).</summary>
    SeriesDayNotBookedRider = 41,
}
