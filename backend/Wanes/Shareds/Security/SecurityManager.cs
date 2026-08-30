using System.Security.Claims;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Shareds.Security;

[ScopedInjectable]
public class SecurityManager : ISecurityManager
{
    private readonly IHttpContextAccessor _http;

    public SecurityManager(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    public int? UserId
    {
        get
        {
            var raw = Principal?.FindFirst(AppClaims.UserId)?.Value
                      ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public int RequireUserId() =>
        UserId ?? throw new AppException(ErrorCode.Unauthorized);

    public IReadOnlyCollection<Roles> Roles =>
        Principal?.FindAll(ClaimTypes.Role)
            .Select(c => Enum.TryParse<Roles>(c.Value, out var r) ? r : (Roles?)null)
            .Where(r => r is not null)
            .Select(r => r!.Value)
            .ToArray() ?? [];

    public bool IsInRole(Roles role) => Roles.Contains(role);

    public string? SessionKey => Principal?.FindFirst(AppClaims.SessionKey)?.Value;
}

public static class AppClaims
{
    public const string UserId = "uid";
    public const string SessionKey = "skey";
}
