using NetTopologySuite.Geometries;
using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Requests;

/// <summary>A rider's open ask when no posted trip matched (the HAIL fallback).</summary>
public class RideRequest : AuditableEntity
{
    public int RiderId { get; set; }
    public User? Rider { get; set; }

    public string OriginAddress { get; set; } = string.Empty;
    public Point Origin { get; set; } = default!;
    public string DestinationAddress { get; set; } = string.Empty;
    public Point Destination { get; set; } = default!;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public int Seats { get; set; } = 1;

    /// <summary>Current driver-notification radius, expands over time.</summary>
    public int RadiusMeters { get; set; } = 2000;

    public RideRequestStatus Status { get; set; } = RideRequestStatus.Open;

    /// <summary>Set when a driver accepted (the resulting booking / trip).</summary>
    public int? MatchedTripId { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
