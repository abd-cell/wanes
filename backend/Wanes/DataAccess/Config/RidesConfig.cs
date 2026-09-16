using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wanes.Areas.Domain.Audit;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Ratings;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Schedules;
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

        // Both optional: a trip a rider posted has no driver and no car until
        // somebody takes it.
        b.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // The driver's board reads trips with no driver by origin and radius.
        b.HasIndex(x => new { x.Status, x.NotifiedAt });
        b.HasMany(x => x.History).WithOne(h => h.Trip)
            .HasForeignKey(h => h.TripId).OnDelete(DeleteBehavior.Cascade);

        // One row per schedule per date — what lets the materialiser run twice,
        // or catch up after an outage, without producing two of Tuesday's trip.
        // Filtered so soft-deleted rows do not hold the slot: a cancelled
        // occurrence should be re-generable if the schedule changes.
        b.HasOne(x => x.Schedule).WithMany().HasForeignKey(x => x.ScheduleId)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.ScheduleId, x.OccurrenceDate })
            .IsUnique()
            .HasFilter("[ScheduleId] IS NOT NULL AND [IsDeleted] = 0");
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
        b.Property(x => x.Route).HasColumnType("geography");

        // Selection is one conditional update, so two drivers cannot both
        // become the active driver. See RideRequest.RowVersion.
        b.Property(x => x.RowVersion).IsRowVersion();

        // What the board, the sweepers and tier 3 all select on.
        b.HasIndex(x => new { x.Status, x.DepartAt });
        b.HasIndex(x => new { x.Status, x.NotifiedAt });

        // The selection sweep: open requests whose window is due.
        b.HasIndex(x => new { x.Status, x.FirstInterestAt });

        // Restrict, not Cascade: the trip a request became is the only thread
        // back to the ride, and deleting a trip must not quietly erase the
        // record of the demand that produced it.
        b.HasOne(x => x.MatchedTrip).WithMany().HasForeignKey(x => x.MatchedTripId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // One request per schedule per date — what lets the materialiser run
        // twice without producing two of Tuesday's. Filtered like the trip's, so
        // a cancelled occurrence can be re-generated if the schedule changes.
        b.HasOne(x => x.Schedule).WithMany().HasForeignKey(x => x.ScheduleId)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.ScheduleId, x.OccurrenceDate })
            .IsUnique()
            .HasFilter("[ScheduleId] IS NOT NULL AND [IsDeleted] = 0");

        b.HasMany(x => x.Participants).WithOne(p => p.RideRequest)
            .HasForeignKey(p => p.RideRequestId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Interests).WithOne(i => i.RideRequest)
            .HasForeignKey(i => i.RideRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RideRequestParticipantConfig : IEntityTypeConfiguration<RideRequestParticipant>
{
    public void Configure(EntityTypeBuilder<RideRequestParticipant> b)
    {
        b.HasOne(x => x.Rider).WithMany().HasForeignKey(x => x.RiderId)
            .OnDelete(DeleteBehavior.Restrict);

        // One active participation per rider per request. Filtered like the
        // booking index and for the same reason: a rider who left and thought
        // better of it may rejoin, and the Left row stays for the history.
        b.HasIndex(x => new { x.RideRequestId, x.RiderId })
            .IsUnique()
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");
    }
}

public class DriverInterestConfig : IEntityTypeConfiguration<DriverInterest>
{
    public void Configure(EntityTypeBuilder<DriverInterest> b)
    {
        b.Property(x => x.PricePerSeat).HasPrecision(10, 2);
        b.Property(x => x.Message).HasMaxLength(300);

        b.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // One live offer per driver per request — a second tap is the same
        // offer, not a second one (idempotency, §13.2).
        b.HasIndex(x => new { x.RideRequestId, x.DriverId })
            .IsUnique()
            .HasFilter("[Status] = 1 AND [IsDeleted] = 0");
    }
}

public class BookingConfig : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> b)
    {
        b.HasOne(x => x.Trip).WithMany().HasForeignKey(x => x.TripId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Rider).WithMany().HasForeignKey(x => x.RiderId)
            .OnDelete(DeleteBehavior.Restrict);

        // One live seat per rider per trip. Filtered rather than absolute: a
        // rider who left and thought better of it may take a seat again, and the
        // cancelled row stays for the history.
        b.HasIndex(x => new { x.TripId, x.RiderId })
            .IsUnique()
            .HasFilter("[Status] <> 4 AND [Status] <> 5 AND [Status] <> 7 AND [IsDeleted] = 0");
    }
}

public class TripScheduleConfig : IEntityTypeConfiguration<TripSchedule>
{
    public void Configure(EntityTypeBuilder<TripSchedule> b)
    {
        b.Property(x => x.OriginAddress).HasMaxLength(300);
        b.Property(x => x.DestinationAddress).HasMaxLength(300);
        b.Property(x => x.Origin).HasColumnType("geography");
        b.Property(x => x.Destination).HasColumnType("geography");
        b.Property(x => x.TimeZoneId).HasMaxLength(64);
        b.Property(x => x.PricePerSeat).HasPrecision(10, 2);

        // What the materialiser pass selects on: live schedules that are not
        // paused, ordered by how far they have been generated.
        b.HasIndex(x => new { x.IsPaused, x.MaterialisedThrough });

        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId)
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
