using Wanes.Areas.Services.Configuration.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Configuration;

[ScopedInjectable]
public interface IAppConfigurationService
{
    /// <summary>The current settings. Never fails: an empty table yields the defaults.</summary>
    Task<BaseResponse<AppConfigurationOutput>> Get();

    /// <summary>Replaces the settings. Admin-only at the controller; audited here.</summary>
    Task<BaseResponse<AppConfigurationOutput>> Update(AppConfigurationInput input);
}
