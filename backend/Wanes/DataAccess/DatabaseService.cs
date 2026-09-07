using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Audit;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Configuration;
using Wanes.Areas.Domain.Logging;
using Wanes.Areas.Domain.Notifications;
using Wanes.Areas.Domain.Ratings;
using Wanes.Areas.Domain.Requests;
using Wanes.Areas.Domain.Support;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;

namespace Wanes.DataAccess;

public class DatabaseService : DbContext
{
    public DatabaseService(DbContextOptions<DatabaseService> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserLogin> UserLogins => Set<UserLogin>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<SavedPlace> SavedPlaces => Set<SavedPlace>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripStatusHistory> TripStatusHistories => Set<TripStatusHistory>();

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<RideRequest> RideRequests => Set<RideRequest>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ApiLog> ApiLogs => Set<ApiLog>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    public DbSet<AppConfiguration> AppConfigurations => Set<AppConfiguration>();

    public DbSet<FaqItem> FaqItems => Set<FaqItem>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DatabaseService).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
