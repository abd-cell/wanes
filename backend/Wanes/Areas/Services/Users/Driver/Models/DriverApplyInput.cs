using System.ComponentModel.DataAnnotations;

namespace Wanes.Areas.Services.Users.Driver.Models;

public class DriverApplyInput
{
    [Required, MaxLength(50)]
    public string LicenseNumber { get; set; } = string.Empty;
}
