using Wanes.Shareds.Enums;

namespace Wanes.Shareds.Security.Token;

public interface ITokenGenerator
{
    /// <summary>Issues a signed access JWT for a login session, with the instant it expires.</summary>
    (string Token, DateTime ExpiresAt) Generate(int userId, IEnumerable<Roles> roles, string sessionKey);

    /// <summary>
    /// Mints a refresh token: the raw value goes to the client, only <c>Hash</c> is stored,
    /// so a leaked database row cannot be replayed against the API.
    /// </summary>
    (string Token, string Hash, DateTime ExpiresAt) GenerateRefreshToken();

    /// <summary>Hashes a client-supplied refresh token for comparison against the stored hash.</summary>
    string HashRefreshToken(string token);
}
