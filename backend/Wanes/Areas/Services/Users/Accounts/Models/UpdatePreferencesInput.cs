using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Users.Accounts.Models;

/// <summary>
/// A partial update: every field is nullable, and null means "leave it alone",
/// so the screen that sends a theme does not have to echo back a language to
/// avoid clobbering it.
///
/// Who the rider travels with used to be in here. It is per-journey now — the
/// search carries who may drive, the posting carries who may share — so there
/// is no preference left to patch.
/// </summary>
public class UpdatePreferencesInput
{
    public Language? Language { get; set; }
    public AppTheme? Theme { get; set; }
    public bool? NotifPush { get; set; }
    public bool? NotifSms { get; set; }
}
