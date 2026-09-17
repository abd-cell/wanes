namespace Wanes.Shareds.Enums;

public enum NotificationType
{
    RiderTripNearby = 1,
    BookingConfirmed = 2,
    TripCancelled = 3,
    DriverAccepted = 4,
    TripCompleted = 5,
    BookingCancelled = 6,
    TripStarted = 7,

    /// <summary>A newly posted trip matched a rider's still-open posting.</summary>
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

    /// <summary>A trip reached the seats its driver asked for, so every held seat is now committed.</summary>
    TripConfirmed = 14,

    /// <summary>A trip was called off for want of riders.</summary>
    TripNotEnoughRiders = 15,

    /// <summary>The driver has to say whether a trip short of its threshold still runs.</summary>
    ConfirmDecision = 16,


    /// <summary>
    /// Something moved on a ride request the user is on — another rider joined,
    /// a driver offered, a driver was chosen. One type for the three because a
    /// client routes them all to the same place: the request, or the trip it
    /// became.
    /// </summary>
    RideRequest = 17,

    /// <summary>A route alert or watched request reached the seats the driver asked for.</summary>
    DemandAlert = 18,

    /// <summary>Something on the user's reliability record changed — a warning, a pause.</summary>
    Reliability = 19,

    /// <summary>A safety report for the admin team.</summary>
    SafetyIncident = 20,

    /// <summary>A recurring commitment moved — an offer, an acceptance, a skipped day, the week ahead.</summary>
    Series = 21,

    General = 100,
}
