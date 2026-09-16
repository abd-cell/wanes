using Microsoft.AspNetCore.Http;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Users.Driver.Models;

/// <summary>
/// One document being uploaded. Multipart rather than JSON: these are phone
/// photographs of a few megabytes, and base64 in a JSON body would inflate them
/// by a third for no gain.
/// </summary>
public class UploadDriverDocumentInput
{
    public DriverDocumentType Type { get; set; }
    public IFormFile? File { get; set; }
}
