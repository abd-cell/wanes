using System.ComponentModel.DataAnnotations;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Notifications.Models;

/// <summary>
/// Sent by the app after it obtains (or refreshes) its FCM registration token.
/// Tokens rotate independently of the login, so this can't be captured only at
/// sign-in.
/// </summary>
public class RegisterDeviceInput
{
    [Required]
    [MaxLength(500)]
    public string DeviceToken { get; set; } = string.Empty;

    /// <summary>Optional — updates the session's platform when supplied.</summary>
    public DeviceType? DeviceType { get; set; }
}
