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

    /// <summary>When the driver last submitted their paperwork for review.</summary>
    public DateTime? DriverAppliedAt { get; set; }

    /// <summary>
    /// Why the admin decided the way they did, in their own words. Shown to the
    /// driver on a rejection — without it "declined" is an instruction to guess,
    /// and the same blurry licence comes back a day later.
    /// </summary>
    public string? DriverReviewNote { get; set; }

    public DateTime? DriverReviewedAt { get; set; }
    public int? DriverReviewedBy { get; set; }

    // reputation
    public double RatingAvg { get; set; }
    public int RatingCount { get; set; }
    public int TripsAsRider { get; set; }
    public int TripsAsDriver { get; set; }

    // reliability — denormalised from ReliabilityEvent, net of waivers, so a
    // trip card can show a completion rate without a query per row.

    /// <summary>Counted and late cancellations of trips riders depended on.</summary>
    public int DriverCancellations { get; set; }

    public int RiderLateCancels { get; set; }
    public int RiderNoShows { get; set; }

    /// <summary>
    /// While set in the future the driver may not take instant requests. Set
    /// when their record crosses the admin's threshold; cleared by time or by
    /// an admin waiving the entries behind it.
    /// </summary>
    public DateTime? SuspendedUntil { get; set; }

    // preferences
    public Language Language { get; set; } = Language.En;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool NotifPush { get; set; } = true;
    public bool NotifSms { get; set; } = true;

    // No ride-with preferences live here, on purpose.
    //
    // Who a rider will travel with used to be account state, edited on a
    // profile screen and applied to every search forever. It is a decision
    // about one journey rather than a standing fact about a person, so it moved
    // to where the journey is described: a search carries who may drive, and a
    // posting carries who may share the car. Nothing about it outlives those,
    // which is why there is nothing left to store.

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
    public ICollection<DriverDocument> DriverDocuments { get; set; } = [];
}
