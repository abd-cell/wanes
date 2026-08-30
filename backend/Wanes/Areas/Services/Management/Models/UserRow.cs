using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>Admin view of a user account (list + detail share one shape).</summary>
public class UserRow
{
    public int Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public bool PhoneVerified { get; set; }
    public string? Email { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public Gender Gender { get; set; }
    public bool IsRider { get; set; }
    public bool IsDriver { get; set; }
    public ActiveRole ActiveRole { get; set; }
    public DriverStatus DriverStatus { get; set; }
    public string? LicenseNumber { get; set; }
    public double RatingAvg { get; set; }
    public int RatingCount { get; set; }
    public int TripsAsRider { get; set; }
    public int TripsAsDriver { get; set; }
    public Language Language { get; set; }
    public bool IsDisabled { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public List<Roles> Roles { get; set; } = [];
    public DateTime CreationDate { get; set; }

    public UserRow() { }

    public UserRow(User u, IEnumerable<Roles> roles)
    {
        Id = u.Id;
        Phone = u.Phone;
        PhoneVerified = u.PhoneVerified;
        Email = u.Email;
        FirstName = u.FirstName;
        LastName = u.LastName;
        DisplayName = u.DisplayName;
        Gender = u.Gender;
        IsRider = u.IsRider;
        IsDriver = u.IsDriver;
        ActiveRole = u.ActiveRole;
        DriverStatus = u.DriverStatus;
        LicenseNumber = u.LicenseNumber;
        RatingAvg = u.RatingAvg;
        RatingCount = u.RatingCount;
        TripsAsRider = u.TripsAsRider;
        TripsAsDriver = u.TripsAsDriver;
        Language = u.Language;
        IsDisabled = u.IsDisabled;
        IsOnline = u.IsOnline;
        LastSeenAt = u.LastSeenAt;
        Roles = roles.ToList();
        CreationDate = u.CreationDate;
    }
}

/// <summary>Create/update payload for a user account.</summary>
public class UserInput
{
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public Gender Gender { get; set; } = Gender.Unspecified;
    public bool IsRider { get; set; } = true;
    public bool IsDriver { get; set; }
    public DriverStatus DriverStatus { get; set; } = DriverStatus.None;
    public string? LicenseNumber { get; set; }
    public Language Language { get; set; } = Language.En;
    public bool IsDisabled { get; set; }
}

/// <summary>Grant or revoke a single role on a user.</summary>
public class RoleInput
{
    public Roles Role { get; set; }
    public bool Grant { get; set; } = true;
}
