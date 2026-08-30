using Microsoft.AspNetCore.Authorization;
using Wanes.Shareds.Enums;

namespace Wanes.Shareds.Attributes;

/// <summary>
/// Role-aware authorization. Usage: <c>[AppAuthorize]</c> (any authenticated user)
/// or <c>[AppAuthorize(Roles.Admin)]</c> to restrict to specific roles.
/// </summary>
public sealed class AppAuthorizeAttribute : AuthorizeAttribute
{
    public AppAuthorizeAttribute(params Roles[] roles)
    {
        if (roles.Length > 0)
            Roles = string.Join(',', roles.Select(r => r.ToString()));
    }
}
