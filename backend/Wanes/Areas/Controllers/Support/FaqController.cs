using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Support;
using Wanes.Areas.Services.Support.Models;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Support;

/// <summary>
/// The published FAQ for the clients.
///
/// Anonymous on purpose, like <see cref="Configuration.ConfigurationController"/>:
/// "how does Wanes work?" is exactly what someone asks before they sign up, so
/// the help screen has to work without a token. Nothing here is account data.
/// </summary>
[Route("api/v1/faq")]
public class FaqController : BaseApiController
{
    private readonly IFaqService service;

    public FaqController(IFaqService service) => this.service = service;

    [HttpGet]
    public async Task<BaseResponse<FaqOutput>> Get() => await service.Get();
}
