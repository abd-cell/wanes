using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Audit;

/// <summary>
/// Immutable, append-only record of every mutating action + auth event.
/// Never updated or deleted. Sensitive values are redacted before storing.
/// </summary>
public class AuditLog : BaseEntity
{
    public int? ActorUserId { get; set; }      // null for system / cron
    public string Action { get; set; } = string.Empty;   // e.g. trip.create, booking.confirm

    public string? EntityType { get; set; }
    public int? EntityId { get; set; }

    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }

    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}
