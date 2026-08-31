namespace Wanes.Shareds.Security.Token;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Wanes";
    public string Audience { get; set; } = "Wanes";

    /// <summary>Access-token lifetime. Short by design — the refresh token carries the session.</summary>
    public int AccessMinutes { get; set; } = 60;

    /// <summary>Refresh-token lifetime, and so how long a device stays signed in while it keeps being used.</summary>
    public int RefreshDays { get; set; } = 60;
}
