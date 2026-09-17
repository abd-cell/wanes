using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Users.Accounts.Models;

public class ProfileOutput
{
    public int Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public bool PhoneVerified { get; set; }
    public string? Email { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public Gender Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Bio { get; set; }
    public bool IsRider { get; set; }
    public bool IsDriver { get; set; }
    public ActiveRole ActiveRole { get; set; }
    public DriverStatus DriverStatus { get; set; }
    public double RatingAvg { get; set; }
    public int RatingCount { get; set; }
    public Language Language { get; set; }
    public AppTheme Theme { get; set; }
    public bool NotifPush { get; set; }

    public bool NotifSms { get; set; }

    /// <summary>
    /// Roles held by the account. The CMS reads this to refuse a non-admin
    /// sign-in up front instead of letting every admin call come back 403.
    /// Filled by the service (roles live in their own table, not on the user).
    /// </summary>
    public List<Roles> Roles { get; set; } = [];

    // safety
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    // reliability — the user's own view of their record
    public int TripsAsDriver { get; set; }
    public int DriverCancellations { get; set; }

    /// <summary>0–1, or null with no history.</summary>
    public double? DriverCompletionRate { get; set; }

    public DateTime? SuspendedUntil { get; set; }

    public ProfileOutput() { }

    public ProfileOutput(User user)
    {
        if (user == null) return;

        Id = user.Id;
        Phone = user.Phone;
        PhoneVerified = user.PhoneVerified;
        Email = user.Email;
        FirstName = user.FirstName;
        LastName = user.LastName;
        DisplayName = user.DisplayName;
        AvatarUrl = user.AvatarUrl;
        Gender = user.Gender;
        DateOfBirth = user.DateOfBirth;
        Bio = user.Bio;
        IsRider = user.IsRider;
        IsDriver = user.IsDriver;
        ActiveRole = user.ActiveRole;
        DriverStatus = user.DriverStatus;
        RatingAvg = user.RatingAvg;
        RatingCount = user.RatingCount;
        Language = user.Language;
        Theme = user.Theme;
        NotifPush = user.NotifPush;
        NotifSms = user.NotifSms;
        EmergencyContactName = user.EmergencyContactName;
        EmergencyContactPhone = user.EmergencyContactPhone;
        TripsAsDriver = user.TripsAsDriver;
        DriverCancellations = user.DriverCancellations;
        DriverCompletionRate = Wanes.Areas.Domain.Marketplace.ReliabilityRules.CompletionRate(
            user.TripsAsDriver, user.DriverCancellations);
        SuspendedUntil = user.SuspendedUntil > DateTime.UtcNow ? user.SuspendedUntil : null;
    }
}
