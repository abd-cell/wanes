using Wanes.Areas.Domain.Vehicles;

namespace Wanes.Areas.Services.Vehicles.Models;

public class VehicleOutput
{
    public int Id { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int? Year { get; set; }
    public int SeatCapacity { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsDefault { get; set; }

    public VehicleOutput() { }

    public VehicleOutput(Vehicle vehicle)
    {
        if (vehicle == null) return;

        Id = vehicle.Id;
        Make = vehicle.Make;
        Model = vehicle.Model;
        Plate = vehicle.Plate;
        Color = vehicle.Color;
        Year = vehicle.Year;
        SeatCapacity = vehicle.SeatCapacity;
        PhotoUrl = vehicle.PhotoUrl;
        IsDefault = vehicle.IsDefault;
    }
}
