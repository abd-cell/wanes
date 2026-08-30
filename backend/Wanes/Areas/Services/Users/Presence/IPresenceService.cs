using Wanes.Areas.Services.Users.Presence.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Users.Presence;

[TransientInjectable]
public interface IPresenceService
{
    Task<BaseResponse> Update(UpdateLocationInput input);
    Task<BaseResponse> GoOffline();
}
