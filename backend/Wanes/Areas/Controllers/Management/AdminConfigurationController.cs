using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Configuration.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Management;

/// <summary>
/// Admin control over the platform settings — currency, brand colour and the
/// support contact channels the app's "Contact us" screen offers. The
/// same values are served anonymously by
/// <see cref="Configuration.ConfigurationController"/> for the clients to read.
/// </summary>
[AppAuthorize(Roles.Admin)]
[Route("api/v1/admin/configuration")]
public class AdminConfigurationController : BaseApiController
{
    private readonly IAppConfigurationService service;

    public AdminConfigurationController(IAppConfigurationService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<AppConfigurationOutput>> Get() => await service.Get();

    [HttpPut]
    public async Task<BaseResponse<AppConfigurationOutput>> Update([FromBody] AppConfigurationInput input)
        => await service.Update(input);
}
