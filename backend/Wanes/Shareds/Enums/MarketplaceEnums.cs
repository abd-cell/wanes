namespace Wanes.Shareds.Enums;

/// <summary>
/// Terms a user has to have read before acting. Versioned on the record, so a
/// rewrite of the copy can ask everybody again.
/// </summary>
public enum AcknowledgementKind
{
    RiderSafety = 1,
    DriverSafety = 2,

    /// <summary>A rider agreeing that their ride is shared and others may join.</summary>
    RiderSharedRide = 3,

    /// <summary>A driver agreeing that the trip they take is shared and its free seats stay on sale.</summary>
    DriverSharedTrip = 4,
}

/// <summary>
/// Why a driver called a trip off. Required once riders depend on it; the
/// safety-flavoured reasons are marked for review so an admin can waive them.
/// </summary>
public enum CancelReason
{
    Personal = 1,
    VehicleProblem = 2,
    SafetyConcern = 3,
    Emergency = 4,
    RouteOrTimeChanged = 5,
    Other = 9,
}

/// <summary>What a reliability record says happened.</summary>
public enum ReliabilityEventKind
{
    /// <summary>A cancellation inside the grace period, or with nobody aboard. Recorded, costs nothing.</summary>
    FreeCancel = 1,

    /// <summary>A driver cancelling a trip riders depend on.</summary>
    Cancel = 2,

    /// <summary>The same, close to departure or after setting off. Weighs double.</summary>
    LateCancel = 3,

    /// <summary>A rider giving a seat back close to departure.</summary>
    RiderLateCancel = 4,

    /// <summary>A rider the driver waited for and who never came.</summary>
    RiderNoShow = 5,

    /// <summary>One day of a series skipped with enough notice. Recorded, costs nothing.</summary>
    SeriesSkip = 6,

    /// <summary>A series ended without the notice period — one per day dropped inside it.</summary>
    SeriesEndShortNotice = 7,
}

/// <summary>Which side of a recurring schedule a commitment is on.</summary>
public enum SeriesSide
{
    /// <summary>A driver drives a rider's recurring request, every day it runs.</summary>
    DriverServes = 1,

    /// <summary>A rider takes a seat on every day of a driver's recurring trip.</summary>
    RiderJoins = 2,
}

public enum SeriesStatus
{
    /// <summary>A driver's offer on a rider's series, waiting for the rider (or the clock).</summary>
    Proposed = 1,

    /// <summary>Running: new days are taken or booked as they are generated.</summary>
    Active = 2,

    /// <summary>The rider picked someone else, or said no.</summary>
    Declined = 3,

    /// <summary>The driver took back the offer before it was decided.</summary>
    Withdrawn = 4,

    Ended = 5,
}

/// <summary>A safety report, and how far the team has got with it.</summary>
public enum SafetyIncidentKind
{
    /// <summary>The emergency button. Always the team's first job.</summary>
    Sos = 1,

    /// <summary>Something that felt wrong, reported after the fact.</summary>
    Report = 2,
}

public enum SafetyIncidentStatus
{
    Open = 1,
    Acknowledged = 2,
    Resolved = 3,
}
