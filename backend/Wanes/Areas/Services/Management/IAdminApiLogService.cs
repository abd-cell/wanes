using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminApiLogService
{
    Task<BaseResponse<PageOutput<ApiLogRow>>> List(PageInput page, int? statusCode, string? method);
    Task<BaseResponse<ApiLogRow>> Get(int id);
}
