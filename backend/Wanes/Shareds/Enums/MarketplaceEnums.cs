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
