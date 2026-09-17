using NetTopologySuite.Geometries;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Marketplace;

/// <summary>
/// A user saying they read something the marketplace asks them to — the safety
/// notes, the fact that a ride is shared. One row per kind per version: asking
/// again after a rewrite is a new row, and the old one stays as the record of
/// what they agreed to then.
/// </summary>
public class UserAcknowledgement : AuditableEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public AcknowledgementKind Kind { get; set; }
    public int Version { get; set; }
    public DateTime AcceptedAt { get; set; }
}

/// <summary>
/// One entry on a user's reliability record — a cancellation, a late one, a
/// no-show.
///
/// There are no payments, so reliability is the only lever the marketplace has
/// on a driver who accepts and then walks away. Each entry carries its points
/// at the time it was written; the rules that produced them live in
/// <see cref="ReliabilityRules"/>, and an admin can waive an entry (a breakdown,
/// a safety call) without deleting the fact that it happened.
/// </summary>
public class ReliabilityEvent : AuditableEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Which side of the car the user was on.</summary>
    public ActiveRole Role { get; set; }

    public ReliabilityEventKind Kind { get; set; }
    public int Points { get; set; }

    public int? TripId { get; set; }
    public int? BookingId { get; set; }

    public CancelReason? Reason { get; set; }
    public string? Note { get; set; }

    /// <summary>How many riders lost their seat because of it.</summary>
    public int RidersAffected { get; set; }

    /// <summary>Minutes between the cancellation and the departure it cancelled.</summary>
    public int MinutesBeforeDeparture { get; set; }

    /// <summary>A reason an admin should look at before it counts against anyone.</summary>
    public bool NeedsReview { get; set; }

    public DateTime? WaivedAt { get; set; }
    public int? WaivedBy { get; set; }
    public string? WaiveNote { get; set; }

    public bool IsWaived => WaivedAt != null;

    /// <summary>What this entry costs today.</summary>
    public int EffectivePoints => IsWaived ? 0 : Points;
}

/// <summary>
/// A driver's standing wish to hear about demand: a route they drive, and how
/// full a request has to be before it is worth a notification.
///
/// With <see cref="RideRequestId"/> set it watches one request instead — the
/// "tell me when this reaches three passengers" on a marketplace card.
/// </summary>
public class DemandAlert : AuditableEntity
{
    public int DriverId { get; set; }
    public User? Driver { get; set; }

    public string OriginAddress { get; set; } = string.Empty;
    public Point Origin { get; set; } = default!;           // SRID 4326
    public string DestinationAddress { get; set; } = string.Empty;
    public Point Destination { get; set; } = default!;      // SRID 4326

    /// <summary>How far from each end a request may start or finish, in metres.</summary>
    public int RadiusMeters { get; set; } = 3000;

    /// <summary>Seats a request needs before it is worth telling the driver.</summary>
    public int MinSeats { get; set; } = 3;

    /// <summary>Set when the alert watches one request rather than a route.</summary>
    public int? RideRequestId { get; set; }
    public RideRequest? RideRequest { get; set; }

    /// <summary>Only recurring requests fire it — for drivers after a regular commute, not one-offs.</summary>
    public bool RecurringOnly { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? LastNotifiedAt { get; set; }
    public int NotifiedCount { get; set; }
}

/// <summary>
/// That an alert already fired for a request. What makes an alert say "3
/// passengers now" once, and not again every time somebody joins.
/// </summary>
public class DemandAlertHit : BaseEntity
{
    public int DemandAlertId { get; set; }
    public DemandAlert? DemandAlert { get; set; }

    public int RideRequestId { get; set; }
}

/// <summary>
/// A safety call — the emergency button, or a report after the fact. Opened by
/// a rider or a driver, worked by the admin team.
/// </summary>
public class SafetyIncident : AuditableEntity
{
    public int ReporterId { get; set; }
    public User? Reporter { get; set; }

    public SafetyIncidentKind Kind { get; set; }
    public SafetyIncidentStatus Status { get; set; } = SafetyIncidentStatus.Open;

    public int? TripId { get; set; }
    public Trip? Trip { get; set; }
    public int? BookingId { get; set; }

    /// <summary>Where the reporter was when they raised it, if the phone knew.</summary>
    public Point? Location { get; set; }

    public string? Note { get; set; }

    /// <summary>Whether the reporter's emergency contact was sent a message.</summary>
    public bool EmergencyContactNotified { get; set; }

    public string? AdminNote { get; set; }
    public int? HandledBy { get; set; }
    public DateTime? HandledAt { get; set; }
}
