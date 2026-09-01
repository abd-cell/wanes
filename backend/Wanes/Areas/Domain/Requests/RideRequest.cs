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

    /// <summary>
    /// The departure the rider searched for — not when they asked.
    ///
    /// A hail used to mean "now", and <see cref="RequestedAt"/> stood in for the
    /// wanted time everywhere it was needed. That is only true of the common
    /// case: a rider who searched for six this evening and matched nothing is
    /// hailing for six, and the trip a driver creates by accepting has to leave
    /// then. Stamped through <see cref="Wanes.Shareds.Constants.MatchRules.HailDepartureFor"/>,
    /// so an immediate hail still carries a real, reachable departure rather
    /// than the instant the rider tapped Search.
    /// </summary>
    public DateTime WantedDepartAt { get; set; } = DateTime.UtcNow;

    public int Seats { get; set; } = 1;

    /// <summary>Current driver-notification radius, expands over time.</summary>
    public int RadiusMeters { get; set; } = 2000;

    public RideRequestStatus Status { get; set; } = RideRequestStatus.Open;

    /// <summary>Set when a driver accepted (the resulting booking / trip).</summary>
    public int? MatchedTripId { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
