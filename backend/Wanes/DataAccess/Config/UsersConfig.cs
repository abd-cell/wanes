using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Extensions;

namespace Wanes.DataAccess.Config;

public class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.Phone).HasMaxLength(20).IsRequired();
        b.HasIndex(u => u.Phone).IsUnique().HasFilter("[IsDeleted] = 0");
        b.Property(u => u.PhoneKey).HasMaxLength(PhoneExtensions.PhoneKeyLength).IsRequired();
        b.HasIndex(u => u.PhoneKey);
        b.Property(u => u.Email).HasMaxLength(200);
        b.Property(u => u.FirstName).HasMaxLength(100);
        b.Property(u => u.LastName).HasMaxLength(100);
        b.Property(u => u.DisplayName).HasMaxLength(150);
        b.Property(u => u.Bio).HasMaxLength(500);
        b.Property(u => u.LastLocation).HasColumnType("geography");

        b.HasMany(u => u.Vehicles).WithOne(v => v.User)
            .HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(u => u.SavedPlaces).WithOne(p => p.User)
            .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class UserRoleConfig : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.HasIndex(x => new { x.UserId, x.Role });
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class UserLoginConfig : IEntityTypeConfiguration<UserLogin>
{
    public void Configure(EntityTypeBuilder<UserLogin> b)
    {
        b.Property(x => x.SessionKey).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.SessionKey);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class OtpCodeConfig : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> b)
    {
        b.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        b.Property(x => x.PhoneKey).HasMaxLength(PhoneExtensions.PhoneKeyLength).IsRequired();
        b.Property(x => x.Code).HasMaxLength(10).IsRequired();
        b.HasIndex(x => x.PhoneKey);
    }
}

public class SavedPlaceConfig : IEntityTypeConfiguration<SavedPlace>
{
    public void Configure(EntityTypeBuilder<SavedPlace> b)
    {
        b.Property(x => x.Name).HasMaxLength(150);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.Location).HasColumnType("geography");
    }
}
