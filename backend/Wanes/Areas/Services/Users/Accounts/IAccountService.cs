using Wanes.Areas.Services.Users.Accounts.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Users.Accounts;

[TransientInjectable]
public interface IAccountService
{
    Task<BaseResponse> RequestOtp(RequestOtpInput input);
    Task<BaseResponse<AuthResult>> VerifyOtp(VerifyOtpInput input);
    Task<BaseResponse> Logout();

    Task<BaseResponse<ProfileOutput>> GetProfile();
    Task<BaseResponse<ProfileOutput>> UpdateProfile(UpdateProfileInput input);
    Task<BaseResponse<ProfileOutput>> UpdatePreferences(UpdatePreferencesInput input);
    Task<BaseResponse<ProfileOutput>> SwitchRole(SwitchRoleInput input);
}
