namespace Wanes.Areas.Services.Vehicles.Models;

public class VehicleInput
{
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int? Year { get; set; }
    public int SeatCapacity { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsDefault { get; set; }
}
