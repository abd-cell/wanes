namespace Wanes.Areas.Services.Users.Accounts.Models;

public class AuthResult
{
    /// <summary>Short-lived bearer token for API calls.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>UTC instant <see cref="Token"/> expires, so a client can refresh ahead of a 401.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Long-lived token that mints the next access token. Rotated on every use.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime RefreshTokenExpiresAt { get; set; }

    public bool IsNewUser { get; set; }
    public ProfileOutput Profile { get; set; } = new();
}
