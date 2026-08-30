using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wanes.Areas.Domain.Logging;

namespace Wanes.DataAccess.Config;

public class ApiLogConfig : IEntityTypeConfiguration<ApiLog>
{
    public void Configure(EntityTypeBuilder<ApiLog> b)
    {
        b.Property(x => x.Method).HasMaxLength(10);
        b.Property(x => x.Path).HasMaxLength(400);
        b.Property(x => x.QueryString).HasMaxLength(1000);
        b.Property(x => x.UserAgent).HasMaxLength(400);
        b.Property(x => x.Ip).HasMaxLength(64);

        // Common lookups: newest-first listing, and filter by actor / status.
        b.HasIndex(x => x.CreationDate);
        b.HasIndex(x => new { x.ActorUserId, x.StatusCode });
    }
}
