using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;

namespace Wanes.Shareds.Security.Token;

[SingletonInjectable]
public class TokenGenerator : ITokenGenerator
{
    private readonly JwtSettings _settings;

    public TokenGenerator(IOptions<JwtSettings> settings) => _settings = settings.Value;

    public string Generate(int userId, IEnumerable<Roles> roles, string sessionKey)
    {
        var claims = new List<Claim>
        {
            new(AppClaims.UserId, userId.ToString()),
            new(AppClaims.SessionKey, sessionKey),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r.ToString())));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(_settings.ExpiryDays),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
