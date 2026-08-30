using Wanes.Shareds.Enums;

namespace Wanes.Shareds.Security;

/// <summary>Reads the authenticated identity (user id, roles) from the current request.</summary>
public interface ISecurityManager
{
    /// <summary>Current user id, or null when unauthenticated.</summary>
    int? UserId { get; }

    /// <summary>Current user id or throws <see cref="Wanes.Shareds.Models.AppException"/> when absent.</summary>
    int RequireUserId();

    IReadOnlyCollection<Roles> Roles { get; }

    bool IsInRole(Roles role);

    /// <summary>The active login session id (jti), used to invalidate tokens on logout.</summary>
    string? SessionKey { get; }
}
