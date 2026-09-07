namespace Wanes.Shareds.Enums;

public enum NotificationType
{
    RideRequestNearby = 1,
    BookingConfirmed = 2,
    TripCancelled = 3,
    DriverAccepted = 4,
    TripCompleted = 5,
    BookingCancelled = 6,
    TripStarted = 7,

    /// <summary>A newly posted trip matched a rider's still-open hail.</summary>
    TripMatched = 8,

    /// <summary>An admin approved the driver application.</summary>
    DriverVerified = 9,

    /// <summary>An admin rejected the driver application.</summary>
    DriverRejected = 10,

    /// <summary>The other party rated a completed booking.</summary>
    RatingReceived = 11,

    /// <summary>The driver reached the pickup point.</summary>
    DriverArrived = 12,

    /// <summary>The support desk answered a complaint or suggestion.</summary>
    FeedbackReplied = 13,

    General = 100,
}
