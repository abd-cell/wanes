using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Users;

/// <summary>
/// One row per active device session. The access token embeds this row's
/// <see cref="SessionKey"/>; logout deletes the row, invalidating the token instantly.
/// The row also holds the (hashed) refresh token that mints the next access token.
/// </summary>
public class UserLogin : BaseEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public string SessionKey { get; set; } = string.Empty;   // jti
    public DeviceType DeviceType { get; set; }
    public string? DeviceToken { get; set; }                 // FCM token
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>SHA-256 of the current refresh token. The raw value is shown to the client once.</summary>
    public string? RefreshTokenHash { get; set; }

    /// <summary>
    /// Hash of the token this one replaced. Kept so a replay can be told apart from an
    /// unknown token: presenting it late means the token leaked, and the session is killed.
    /// </summary>
    public string? PreviousRefreshTokenHash { get; set; }

    /// <summary>When the current refresh token stops being accepted — the real session length.</summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }

    /// <summary>Last rotation, which opens the short grace window for a racing retry.</summary>
    public DateTime? RefreshRotatedAt { get; set; }
}
