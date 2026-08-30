using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Users;

/// <summary>Account + full profile. Phone is the verified identity.</summary>
public class User : AuditableEntity
{
    // identity
    public string Phone { get; set; } = string.Empty;         // unique, E.164
    public string PhoneKey { get; set; } = string.Empty;      // last 9 digits — what auth matches on
    public bool PhoneVerified { get; set; }
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string? PasswordHash { get; set; }

    // profile
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public Gender Gender { get; set; } = Gender.Unspecified;
    public DateTime? DateOfBirth { get; set; }
    public string? Bio { get; set; }

    // roles
    public bool IsRider { get; set; } = true;
    public bool IsDriver { get; set; }
    public ActiveRole ActiveRole { get; set; } = ActiveRole.Rider;

    // driver onboarding / trust
    public DriverStatus DriverStatus { get; set; } = DriverStatus.None;
    public string? LicenseNumber { get; set; }
    public string? LicensePhotoUrl { get; set; }
    public string? IdDocumentUrl { get; set; }

    // reputation
    public double RatingAvg { get; set; }
    public int RatingCount { get; set; }
    public int TripsAsRider { get; set; }
    public int TripsAsDriver { get; set; }

    // preferences
    public Language Language { get; set; } = Language.En;
    public bool NotifPush { get; set; } = true;
    public bool NotifSms { get; set; } = true;

    // safety
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    // account
    public bool IsDisabled { get; set; }
    public DateTime? LastSeenAt { get; set; }

    // driver presence (for hail targeting)
    public bool IsOnline { get; set; }
    public NetTopologySuite.Geometries.Point? LastLocation { get; set; }  // SRID 4326
    public DateTime? LastLocationAt { get; set; }

    // navigation
    public ICollection<Vehicles.Vehicle> Vehicles { get; set; } = [];
    public ICollection<SavedPlace> SavedPlaces { get; set; } = [];
}
