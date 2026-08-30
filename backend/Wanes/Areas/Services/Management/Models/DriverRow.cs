using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Management.Models;

public class DriverRow
{
    public int Id { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DriverStatus DriverStatus { get; set; }
    public string? LicenseNumber { get; set; }
}
