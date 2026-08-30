namespace Wanes.Shareds.Security.Token;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Wanes";
    public string Audience { get; set; } = "Wanes";
    public int ExpiryDays { get; set; } = 30;
}
