namespace Wanes.Shareds.Enums;

public enum TripStatus
{
    Posted = 1,
    Full = 2,
    Active = 3,
    Completed = 4,
    Cancelled = 5,

    /// <summary>Driver is at the pickup point, waiting for the rider to board.</summary>
    Arrived = 6,
}
