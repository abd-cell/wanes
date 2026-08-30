using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Users.Accounts.Models;

public class UpdatePreferencesInput
{
    public Language? Language { get; set; }
    public bool? NotifPush { get; set; }
    public bool? NotifSms { get; set; }
}
