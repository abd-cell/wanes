using Wanes.Areas.Services.Management.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

[TransientInjectable]
public interface IAdminUserService
{
    Task<BaseResponse<PageOutput<UserRow>>> List(PageInput page, DriverStatus? driverStatus, bool? isDriver, bool? isDisabled);
    Task<BaseResponse<UserRow>> Get(int id);
    Task<BaseResponse<UserRow>> Create(UserInput input);
    Task<BaseResponse<UserRow>> Update(int id, UserInput input);
    Task<BaseResponse> Delete(int id);
    Task<BaseResponse<UserRow>> SetRole(int id, RoleInput input);
}
