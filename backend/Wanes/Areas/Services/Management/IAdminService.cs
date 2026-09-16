using Wanes.Areas.Services.Management.Models;
using Wanes.Areas.Services.Users.Driver.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminService
{
    /// <summary>
    /// The verification queue. Defaults to applications awaiting a decision;
    /// <paramref name="status"/> reopens the ones already decided, which is the
    /// only way back to a driver who was rejected by a mis-click.
    /// </summary>
    Task<BaseResponse<PageOutput<DriverRow>>> GetPendingDrivers(PageInput page, DriverStatus? status = null);

    /// <summary>The documents one driver uploaded, for the reviewer to look at.</summary>
    Task<BaseResponse<List<DriverDocumentOutput>>> GetDriverDocuments(int userId);

    /// <summary>
    /// Opens one driver document for an admin. Audited: reading someone's
    /// identity papers is an act the platform should be able to account for.
    /// </summary>
    Task<BaseResponse<DocumentFile>> OpenDriverDocument(int documentId);

    Task<BaseResponse> VerifyDriver(int userId, VerifyInput input);
    Task<BaseResponse<PageOutput<AuditRow>>> GetAuditLog(PageInput page, int? actorUserId, string? action);
}
