namespace Wanes.Areas.Services.Users.Accounts.Models;

public class AuthResult
{
    public string Token { get; set; } = string.Empty;
    public bool IsNewUser { get; set; }
    public ProfileOutput Profile { get; set; } = new();
}
