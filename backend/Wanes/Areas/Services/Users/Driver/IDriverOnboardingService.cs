using Wanes.Areas.Services.Users.Driver.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Users.Driver;

[TransientInjectable]
public interface IDriverOnboardingService
{
    Task<BaseResponse> Apply(DriverApplyInput input);
}
