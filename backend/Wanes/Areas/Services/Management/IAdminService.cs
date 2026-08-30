using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminService
{
    Task<BaseResponse<PageOutput<DriverRow>>> GetPendingDrivers(PageInput page);
    Task<BaseResponse> VerifyDriver(int userId, VerifyInput input);
    Task<BaseResponse<PageOutput<AuditRow>>> GetAuditLog(PageInput page, int? actorUserId, string? action);
}
