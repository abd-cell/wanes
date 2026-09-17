using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Users.Accounts.Models;

public class UpdateProfileInput
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public Gender? Gender { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Bio { get; set; }

    /// <summary>Who the SOS button messages. Empty clears it.</summary>
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string? EmergencyContactName { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(32)]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^$|^\+?[0-9][0-9\s\-()]{4,}$",
        ErrorMessage = "EmergencyContactPhone must be a phone number.")]
    public string? EmergencyContactPhone { get; set; }
}
