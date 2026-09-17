using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wanes.Areas.Domain.Marketplace;

namespace Wanes.DataAccess.Config;

public class UserAcknowledgementConfig : IEntityTypeConfiguration<UserAcknowledgement>
{
    public void Configure(EntityTypeBuilder<UserAcknowledgement> b)
    {
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        // One agreement per user per kind per version — agreeing twice is the same agreement.
        b.HasIndex(x => new { x.UserId, x.Kind, x.Version })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}

public class ReliabilityEventConfig : IEntityTypeConfiguration<ReliabilityEvent>
{
    public void Configure(EntityTypeBuilder<ReliabilityEvent> b)
    {
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.WaiveNote).HasMaxLength(500);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        // The standing query: a user's recent entries.
        b.HasIndex(x => new { x.UserId, x.CreationDate });
    }
}

public class DemandAlertConfig : IEntityTypeConfiguration<DemandAlert>
{
    public void Configure(EntityTypeBuilder<DemandAlert> b)
    {
        b.Property(x => x.OriginAddress).HasMaxLength(300);
        b.Property(x => x.DestinationAddress).HasMaxLength(300);
        b.Property(x => x.Origin).HasColumnType("geography");
        b.Property(x => x.Destination).HasColumnType("geography");

        b.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.RideRequest).WithMany().HasForeignKey(x => x.RideRequestId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.IsActive, x.RideRequestId });
        b.HasIndex(x => x.DriverId);
    }
}

public class DemandAlertHitConfig : IEntityTypeConfiguration<DemandAlertHit>
{
    public void Configure(EntityTypeBuilder<DemandAlertHit> b)
    {
        b.HasOne(x => x.DemandAlert).WithMany().HasForeignKey(x => x.DemandAlertId)
            .OnDelete(DeleteBehavior.Cascade);

        // An alert fires once per request.
        b.HasIndex(x => new { x.DemandAlertId, x.RideRequestId }).IsUnique();
    }
}

public class SafetyIncidentConfig : IEntityTypeConfiguration<SafetyIncident>
{
    public void Configure(EntityTypeBuilder<SafetyIncident> b)
    {
        b.Property(x => x.Location).HasColumnType("geography");
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.AdminNote).HasMaxLength(1000);

        b.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Trip).WithMany().HasForeignKey(x => x.TripId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // The admin queue: open work first.
        b.HasIndex(x => new { x.Status, x.CreationDate });
    }
}

public class SeriesCommitmentConfig : IEntityTypeConfiguration<Wanes.Areas.Domain.Series.SeriesCommitment>
{
    public void Configure(EntityTypeBuilder<Wanes.Areas.Domain.Series.SeriesCommitment> b)
    {
        b.Property(x => x.PricePerSeat).HasPrecision(10, 2);
        b.Property(x => x.Message).HasMaxLength(300);
        b.Property(x => x.EndNote).HasMaxLength(500);

        b.HasOne(x => x.Schedule).WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Rider).WithMany().HasForeignKey(x => x.RiderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // What the materialiser asks as it writes a day, and the clocks sweep.
        b.HasIndex(x => new { x.ScheduleId, x.Side, x.Status });
        b.HasIndex(x => new { x.Status, x.DecideAt });
        b.HasIndex(x => x.DriverId);
        b.HasIndex(x => x.RiderId);
    }
}
