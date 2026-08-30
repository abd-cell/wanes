using Microsoft.AspNetCore.Mvc;
using Wanes.Areas.Services.Users.Presence;
using Wanes.Areas.Services.Users.Presence.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Controllers.Users;

[AppAuthorize]
[Route("api/v1/me/location")]
public class PresenceController : BaseApiController
{
    private readonly IPresenceService presenceService;

    public PresenceController(IPresenceService presenceService) => this.presenceService = presenceService;

    /// <summary>Driver reports current location + online state (for hail targeting).</summary>
    [HttpPost]
    public async Task<BaseResponse> Update([FromBody] UpdateLocationInput input)
        => await presenceService.Update(input);

    [HttpPost("offline")]
    public async Task<BaseResponse> Offline() => await presenceService.GoOffline();
}
