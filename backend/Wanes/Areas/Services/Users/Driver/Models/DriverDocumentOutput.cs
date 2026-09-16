using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Users.Driver.Models;

/// <summary>
/// A document as a client sees it. Carries no storage key and no URL of its
/// own: the bytes come from the content endpoint, which checks who is asking.
/// </summary>
public class DriverDocumentOutput
{
    public int Id { get; set; }
    public DriverDocumentType Type { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }

    /// <summary>True for a PDF, so a viewer knows not to try rendering it as an image.</summary>
    public bool IsPdf => ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

    public static DriverDocumentOutput From(DriverDocument d) => new()
    {
        Id = d.Id,
        Type = d.Type,
        FileName = d.FileName,
        ContentType = d.ContentType,
        SizeBytes = d.SizeBytes,
        UploadedAt = d.CreationDate,
    };
}
