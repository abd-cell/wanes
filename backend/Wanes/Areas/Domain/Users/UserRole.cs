using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Users;

/// <summary>Assigns a <see cref="Roles"/> to a user (a user may hold several).</summary>
public class UserRole : BaseEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public Roles Role { get; set; } = Roles.User;
}
