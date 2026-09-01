using Wanes.Areas.Services.Users.Presence.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Users.Presence;

[TransientInjectable]
public interface IPresenceService
{
    /// <summary>
    /// Reports where the driver is and whether they want rides. The location is
    /// always stored; the online flag cannot be raised while the driver is out on
    /// a trip.
    /// </summary>
    Task<BaseResponse> Update(UpdateLocationInput input);
    Task<BaseResponse> GoOffline();
}
