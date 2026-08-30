using Wanes.Areas.Domain.Vehicles;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>Admin view of a vehicle (list + detail share one shape).</summary>
public class VehicleRow
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? OwnerName { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int? Year { get; set; }
    public int SeatCapacity { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreationDate { get; set; }

    public VehicleRow() { }

    public VehicleRow(Vehicle e)
    {
        Id = e.Id;
        UserId = e.UserId;
        Make = e.Make;
        Model = e.Model;
        Plate = e.Plate;
        Color = e.Color;
        Year = e.Year;
        SeatCapacity = e.SeatCapacity;
        PhotoUrl = e.PhotoUrl;
        IsDefault = e.IsDefault;
        CreationDate = e.CreationDate;
    }
}

/// <summary>Create/update payload for a vehicle.</summary>
public class VehicleInput
{
    public int UserId { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int? Year { get; set; }
    public int SeatCapacity { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsDefault { get; set; }
}
