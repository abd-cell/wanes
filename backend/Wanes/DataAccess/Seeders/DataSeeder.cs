using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;

namespace Wanes.DataAccess.Seeders;

/// <summary>Seeds a default admin account on first run (idempotent).</summary>
public static class DataSeeder
{
    public const string AdminPhone = "+962790000000";

    public static async Task SeedAsync(DatabaseService db)
    {
        var adminKey = AdminPhone.PhoneKey();
        var admin = await db.Users.FirstOrDefaultAsync(u => u.PhoneKey == adminKey);
        if (admin is null)
        {
            admin = new User
            {
                Phone = AdminPhone,
                PhoneKey = adminKey,
                PhoneVerified = true,
                FirstName = "Wanes",
                LastName = "Admin",
                DisplayName = "Admin",
                IsRider = false,
                IsDriver = false,
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }

        var hasAdminRole = await db.UserRoles.AnyAsync(r => r.UserId == admin.Id && r.Role == Roles.Admin);
        if (!hasAdminRole)
        {
            db.UserRoles.Add(new UserRole { UserId = admin.Id, Role = Roles.Admin });
            await db.SaveChangesAsync();
        }
    }
}
