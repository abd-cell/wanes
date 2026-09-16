using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Users.Driver.Models;

/// <summary>
/// Everything the driver's own verification screen needs in one call: where the
/// application stands, what has been uploaded, and what is still missing.
///
/// <see cref="MissingTypes"/> is computed server-side on purpose. The required
/// set is a platform rule, and a client that hardcodes its own copy will one day
/// let a driver submit an application the API then refuses.
/// </summary>
public class DriverVerificationOutput
{
    public DriverStatus Status { get; set; }
    public string? LicenseNumber { get; set; }
    public DateTime? AppliedAt { get; set; }

    /// <summary>The reviewer's note. Populated on a rejection; that is the whole point of it.</summary>
    public string? ReviewNote { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public List<DriverDocumentOutput> Documents { get; set; } = [];
    public List<DriverDocumentType> MissingTypes { get; set; } = [];

    /// <summary>True once every required document is on file — what gates Apply.</summary>
    public bool CanSubmit => MissingTypes.Count == 0;
}
