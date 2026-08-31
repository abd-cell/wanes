namespace Wanes.Areas.Services.Trips.Models;

/// <summary>
/// Where the driver last reported themselves, for the rider's tracking map.
///
/// Readable only by the trip's own driver or a rider holding a live seat on it
/// — the same rule that governs the phone numbers the two sides exchange. A
/// driver's position is not public, and stops being shared the moment the seat
/// is cancelled or the trip is done.
/// </summary>
public class DriverLocationOutput
{
    public double Lat { get; set; }
    public double Lng { get; set; }

    /// <summary>When the driver's app last reported. Null if it never has.</summary>
    public DateTime? ReportedAt { get; set; }

    /// <summary>Whether the driver's app is currently reporting at all.</summary>
    public bool Online { get; set; }
}
