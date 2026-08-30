using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Users;

/// <summary>
/// One row per active device session. The token embeds this row's <see cref="SessionKey"/>;
/// logout deletes the row, invalidating the token instantly.
/// </summary>
public class UserLogin : BaseEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public string SessionKey { get; set; } = string.Empty;   // jti
    public DeviceType DeviceType { get; set; }
    public string? DeviceToken { get; set; }                 // FCM token
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}
