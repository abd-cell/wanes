using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;

namespace Wanes.Areas.Services.Management;

/// <summary>
/// Resolves a page of user ids to display names in one query. Used by the rows that
/// hold a bare foreign key with no navigation property (notifications, API logs,
/// audit entries) so the CMS can show who a record belongs to instead of a number.
/// Deleted users are included — a log entry still names its actor after the account
/// is removed.
/// </summary>
internal static class AdminUserNames
{
    public static async Task<Dictionary<int, string>> ResolveNames(
        this IRepository<User> userRepository, IEnumerable<int?> userIds)
    {
        var wanted = userIds.Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
        if (wanted.Count == 0) return new Dictionary<int, string>();

        var users = await userRepository.Query(includeDeleted: true)
            .Where(u => wanted.Contains(u.Id))
            .ToListAsync();

        return users.ToDictionary(u => u.Id, u => AdminLabels.ForUser(u) ?? $"#{u.Id}");
    }

    /// <summary>Name for a nullable foreign key, or null when it is unset / unknown.</summary>
    public static string? NameFor(this Dictionary<int, string> names, int? userId) =>
        userId != null && names.TryGetValue(userId.Value, out var name) ? name : null;
}
