using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wanes.Areas.Domain.Audit;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Ratings;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Vehicles;

namespace Wanes.DataAccess.Config;

public class VehicleConfig : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b)
    {
        b.Property(x => x.Make).HasMaxLength(80);
        b.Property(x => x.Model).HasMaxLength(80);
        b.Property(x => x.Plate).HasMaxLength(20);
        b.Property(x => x.Color).HasMaxLength(40);
    }
}

public class TripConfig : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> b)
    {
        b.Property(x => x.OriginAddress).HasMaxLength(300);
        b.Property(x => x.DestinationAddress).HasMaxLength(300);
        b.Property(x => x.Origin).HasColumnType("geography");
        b.Property(x => x.Destination).HasColumnType("geography");
        b.Property(x => x.Route).HasColumnType("geography");
        b.Property(x => x.PricePerSeat).HasPrecision(10, 2);

        b.HasIndex(x => new { x.Status, x.DepartAt });

        // Makes the seat decrement a conditional update rather than a
        // read-then-write. See Trip.RowVersion.
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne(h => h.Trip)
            .HasForeignKey(h => h.TripId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class BookingConfig : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.HasIndex(x => new { x.TripId, x.RiderId });
        b.HasOne(x => x.Trip).WithMany().HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Rider).WithMany().HasForeignKey(x => x.RiderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RideRequestConfig : IEntityTypeConfiguration<RideRequest>
{
    public void Configure(EntityTypeBuilder<RideRequest> b)
    {
        b.Property(x => x.OriginAddress).HasMaxLength(300);
        b.Property(x => x.DestinationAddress).HasMaxLength(300);
        b.Property(x => x.Origin).HasColumnType("geography");
        b.Property(x => x.Destination).HasColumnType("geography");
        b.HasIndex(x => x.Status);

        // Makes "first driver to accept wins" an actual claim. See RideRequest.RowVersion.
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Rider).WithMany().HasForeignKey(x => x.RiderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RatingConfig : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> b)
    {
        b.Property(x => x.Comment).HasMaxLength(500);
        b.HasIndex(x => new { x.BookingId, x.Direction }).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FromUser).WithMany().HasForeignKey(x => x.FromUserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ToUser).WithMany().HasForeignKey(x => x.ToUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.Property(x => x.Action).HasMaxLength(100).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100);
        b.Property(x => x.Ip).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(400);
        b.HasIndex(x => new { x.ActorUserId, x.CreationDate });
        b.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
