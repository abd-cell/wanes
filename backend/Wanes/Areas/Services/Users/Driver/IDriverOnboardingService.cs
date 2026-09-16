using Wanes.Areas.Services.Users.Driver.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Users.Driver;

[TransientInjectable]
public interface IDriverOnboardingService
{
    /// <summary>Where the caller's own application stands, with its documents.</summary>
    Task<BaseResponse<DriverVerificationOutput>> GetStatus();

    /// <summary>
    /// Stores one document for the caller. Uploading a type they already hold
    /// replaces it — a retake of a blurry photo is the common case, and leaving
    /// both would make the reviewer guess which one counts.
    /// </summary>
    Task<BaseResponse<DriverDocumentOutput>> UploadDocument(UploadDriverDocumentInput input);

    Task<BaseResponse> DeleteDocument(int id);

    /// <summary>Opens one of the caller's own documents.</summary>
    Task<BaseResponse<DocumentFile>> OpenDocument(int id);

    /// <summary>Submits the application for manual review, once the paperwork is complete.</summary>
    Task<BaseResponse> Apply(DriverApplyInput input);
}
