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
    RideRequestNearbyDriver = 1,
    BookingConfirmedRider = 2,
    BookingConfirmedDriver = 3,
    BookingCancelledDriver = 4,
    TripCancelledRider = 5,
    TripStartedRider = 6,
    TripCompletedRider = 7,
    TripMatchedRider = 8,
    DriverAcceptedRider = 9,
    DriverVerified = 10,
    DriverRejected = 11,
    RatingReceived = 12,
    DriverArrivedRider = 13,

    /// <summary>The driver waited at the pickup and marked the rider a no-show.</summary>
    BookingNoShowRider = 14,

    /// <summary>The support desk answered the user's complaint or suggestion.</summary>
    FeedbackReplied = 15,
}
