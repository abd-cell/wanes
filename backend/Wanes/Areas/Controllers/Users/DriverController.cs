using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Users.Driver;
using Wanes.Areas.Services.Users.Driver.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Users;

[AppAuthorize]
[Route("api/v1/me/driver")]
public class DriverController : BaseApiController
{
    private readonly IDriverOnboardingService driverOnboardingService;

    public DriverController(IDriverOnboardingService driverOnboardingService)
        => this.driverOnboardingService = driverOnboardingService;

    /// <summary>Submit license + id documents for driver verification.</summary>
    [HttpPost("apply")]
    public async Task<BaseResponse> Apply([FromBody] DriverApplyInput input)
        => await driverOnboardingService.Apply(input);
}
