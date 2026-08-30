namespace Wanes.Areas.Services.Management.Models;

public class AuditRow
{
    public int Id { get; set; }
    public int? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? Ip { get; set; }
    public DateTime CreationDate { get; set; }
}
