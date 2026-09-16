namespace Wanes.Areas.Services.Users.Driver.Models;

/// <summary>
/// A stored file opened for reading. The one thing in this codebase that leaves
/// a controller outside the <c>BaseResponse</c> envelope — a JPEG has no place
/// in a JSON field. Failures still come back as the usual envelope; only the
/// success path is binary.
/// </summary>
public class DocumentFile
{
    public required Stream Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
}
