using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;

namespace Wanes.Shareds.Security.Token;

[SingletonInjectable]
public class TokenGenerator : ITokenGenerator
{
    /// <summary>Refresh-token entropy. 32 bytes is well past guessing range.</summary>
    private const int RefreshTokenBytes = 32;

    private readonly JwtSettings _settings;

    public TokenGenerator(IOptions<JwtSettings> settings) => _settings = settings.Value;

    public (string Token, DateTime ExpiresAt) Generate(int userId, IEnumerable<Roles> roles, string sessionKey)
    {
        var claims = new List<Claim>
        {
            new(AppClaims.UserId, userId.ToString()),
            new(AppClaims.SessionKey, sessionKey),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r.ToString())));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public (string Token, string Hash, DateTime ExpiresAt) GenerateRefreshToken()
    {
        // URL-safe so the value survives being carried in a header, body, or query string.
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
        return (raw, HashRefreshToken(raw), DateTime.UtcNow.AddDays(_settings.RefreshDays));
    }

    // A plain SHA-256 rather than a work-factored password hash: the input is 256 bits of
    // randomness, not a guessable secret, so there is nothing for a brute force to bite on.
    public string HashRefreshToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
