using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wanes.Areas.Domain.Configuration;

namespace Wanes.DataAccess.Config;

public class AppConfigurationConfig : IEntityTypeConfiguration<AppConfiguration>
{
    public void Configure(EntityTypeBuilder<AppConfiguration> b)
    {
        b.Property(x => x.CurrencyCode).HasMaxLength(8).IsRequired();
        b.Property(x => x.CurrencySymbol).HasMaxLength(8).IsRequired();
        // "#RRGGBB".
        b.Property(x => x.PrimaryColor).HasMaxLength(9).IsRequired();

        // Support channels — all optional, so no IsRequired().
        b.Property(x => x.SupportPhone).HasMaxLength(32);
        b.Property(x => x.SupportWhatsApp).HasMaxLength(32);
        b.Property(x => x.SupportEmail).HasMaxLength(256);
        b.Property(x => x.SupportWebsite).HasMaxLength(512);
        b.Property(x => x.SupportHours).HasMaxLength(200);

        // Safety. The existing row picks up the shipped defaults from the
        // migration's column defaults, not from here — a model default on a
        // bool would stop EF ever writing `false`.
        b.Property(x => x.EmergencyNumber).HasMaxLength(16).IsRequired();
        b.Property(x => x.ShareBaseUrl).HasMaxLength(512);
    }
}
