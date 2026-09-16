using Wanes.Shareds.Enums;

namespace Wanes.Shareds.Constants;

/// <summary>
/// What the platform will accept as a driver document, in one place: the app
/// disables its picker on the same numbers the API enforces, so the two must
/// agree.
/// </summary>
public static class DriverDocumentRules
{
    /// <summary>
    /// 8 MB. A phone photograph of a licence lands around 2-4 MB; the ceiling is
    /// there to stop a video or a scanned book, not to make the driver retake
    /// the picture.
    /// </summary>
    public const long MaxBytes = 8 * 1024 * 1024;

    /// <summary>
    /// Types an application cannot be reviewed without. Everything else in
    /// <see cref="DriverDocumentType"/> is welcome but optional.
    /// </summary>
    public static readonly DriverDocumentType[] Required =
    [
        DriverDocumentType.LicenseFront,
        DriverDocumentType.LicenseBack,
        DriverDocumentType.IdDocument,
    ];

    /// <summary>
    /// Accepted content types mapped to the extension the file is stored under.
    /// The uploader's own file name never reaches the disk — see
    /// <c>LocalFileStorage</c> — so this is also the only source of extensions.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AllowedTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["image/heic"] = ".heic",
            ["application/pdf"] = ".pdf",
        };

    public static bool IsAllowed(string? contentType) =>
        contentType != null && AllowedTypes.ContainsKey(contentType);

    public static string ExtensionFor(string contentType) => AllowedTypes[contentType];

    /// <summary>
    /// Checks the bytes actually start the way the declared type says they
    /// should. The content type on a multipart part is whatever the client typed
    /// there, so on its own it proves nothing: this is what stops an executable
    /// arriving labelled <c>image/png</c> and sitting on disk until someone
    /// opens it. HEIC is exempt — it is an ISO-BMFF container whose brand varies
    /// by encoder, and a half-right signature test would reject real photos.
    /// </summary>
    public static bool MatchesSignature(string contentType, ReadOnlySpan<byte> head) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
        "image/png" => head.Length >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47,
        "image/webp" => head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8),
        "application/pdf" => head.Length >= 5 && head[..5].SequenceEqual("%PDF-"u8),
        _ => true,
    };
}
