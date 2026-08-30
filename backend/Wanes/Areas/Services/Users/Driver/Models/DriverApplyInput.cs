namespace Wanes.Areas.Services.Users.Driver.Models;

public class DriverApplyInput
{
    public string LicenseNumber { get; set; } = string.Empty;
    public string LicensePhotoUrl { get; set; } = string.Empty;
    public string IdDocumentUrl { get; set; } = string.Empty;
}
