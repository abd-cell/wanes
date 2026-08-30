using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminUserLoginService
{
    Task<BaseResponse<PageOutput<UserLoginRow>>> List(PageInput page, DeviceType? deviceType, int? userId);
    Task<BaseResponse<UserLoginRow>> Get(int id);
    Task<BaseResponse> Delete(int id);
}
