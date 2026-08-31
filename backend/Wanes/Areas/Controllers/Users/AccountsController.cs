using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Users.Accounts;
using Wanes.Areas.Services.Users.Accounts.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Users;

public class AccountsController : BaseApiController
{
    private readonly IAccountService accountService;

    public AccountsController(IAccountService accountService) => this.accountService = accountService;

    [HttpPost("request-otp")]
    public async Task<BaseResponse> RequestOtp([FromBody] RequestOtpInput input)
        => await accountService.RequestOtp(input);

    [HttpPost("verify-otp")]
    public async Task<BaseResponse<AuthResult>> VerifyOtp([FromBody] VerifyOtpInput input)
        => await accountService.VerifyOtp(input);

    /// <summary>Exchanges a refresh token for a new access token. Anonymous: the caller's
    /// access token has expired by the time this is needed.</summary>
    [HttpPost("refresh")]
    public async Task<BaseResponse<AuthResult>> Refresh([FromBody] RefreshTokenInput input)
        => await accountService.Refresh(input);

    [AppAuthorize]
    [HttpPost("logout")]
    public async Task<BaseResponse> Logout() => await accountService.Logout();

    [AppAuthorize]
    [HttpGet("me")]
    public async Task<BaseResponse<ProfileOutput>> Me() => await accountService.GetProfile();

    [AppAuthorize]
    [HttpPatch("me")]
    public async Task<BaseResponse<ProfileOutput>> UpdateMe([FromBody] UpdateProfileInput input)
        => await accountService.UpdateProfile(input);

    [AppAuthorize]
    [HttpPatch("me/preferences")]
    public async Task<BaseResponse<ProfileOutput>> UpdatePreferences([FromBody] UpdatePreferencesInput input)
        => await accountService.UpdatePreferences(input);

    [AppAuthorize]
    [HttpPatch("me/role")]
    public async Task<BaseResponse<ProfileOutput>> SwitchRole([FromBody] SwitchRoleInput input)
        => await accountService.SwitchRole(input);
}
