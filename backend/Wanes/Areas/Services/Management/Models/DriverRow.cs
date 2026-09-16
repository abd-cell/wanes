using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Management.Models;

public class DriverRow
{
    public int Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DriverStatus DriverStatus { get; set; }
    public string? LicenseNumber { get; set; }

    /// <summary>When they submitted, so the queue can be worked oldest-first.</summary>
    public DateTime? AppliedAt { get; set; }

    /// <summary>How many documents are on file — a row showing 0 needs no opening.</summary>
    public int DocumentCount { get; set; }

    /// <summary>The note left on the last decision, when there was one.</summary>
    public string? ReviewNote { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
