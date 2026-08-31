using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Configuration.Models;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Configuration;

/// <summary>
/// Read-only view of the platform settings for the clients.
///
/// Anonymous on purpose: the mobile app paints the splash and the sign-in screen
/// in the brand colour before anyone has a token. Nothing here is account data.
/// </summary>
[Route("api/v1/configuration")]
public class ConfigurationController : BaseApiController
{
    private readonly IAppConfigurationService service;

    public ConfigurationController(IAppConfigurationService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<AppConfigurationOutput>> Get() => await service.Get();
}
