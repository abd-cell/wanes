using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Users;

/// <summary>
/// One file a driver uploaded to prove they may drive — a licence face, an id,
/// a registration.
///
/// A row per file rather than a pair of URL columns on <see cref="User"/>, which
/// is what this replaced. The old shape could hold exactly two documents, had
/// nowhere to record what was actually uploaded (name, type, size, when), and
/// pointed at an address the platform did not own. Verification is a review of
/// evidence, and evidence needs a list.
///
/// The bytes live in <see cref="Wanes.Shareds.Files.IFileStorage"/> under
/// <see cref="StorageKey"/>; nothing serves them without checking who is asking.
/// Replacing a document soft-deletes the old row instead of overwriting it, so
/// an approval can still be traced back to the file the admin actually saw.
/// </summary>
public class DriverDocument : AuditableEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public DriverDocumentType Type { get; set; }

    /// <summary>Opaque key into the file storage. Never returned to a client.</summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>
    /// The name the file arrived with. Shown to the reviewer as a hint and
    /// nothing more — it takes no part in the path on disk.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}
