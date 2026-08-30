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
}
