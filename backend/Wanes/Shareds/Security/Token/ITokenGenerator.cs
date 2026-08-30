using Wanes.Shareds.Enums;

namespace Wanes.Shareds.Security.Token;

public interface ITokenGenerator
{
    /// <summary>Issues a signed JWT for a login session.</summary>
    string Generate(int userId, IEnumerable<Roles> roles, string sessionKey);
}
