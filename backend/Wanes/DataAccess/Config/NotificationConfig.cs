using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wanes.Areas.Domain.Notifications;

namespace Wanes.DataAccess.Config;

public class UserNotificationConfig : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> b)
    {
        b.Property(x => x.Title).HasMaxLength(150);
        b.Property(x => x.Body).HasMaxLength(500);
        b.HasIndex(x => new { x.UserId, x.IsRead });
    }
}
