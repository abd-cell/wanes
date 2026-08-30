using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Vehicles;

/// <summary>A driver's car. <see cref="SeatCapacity"/> is the physical max passenger count.</summary>
public class Vehicle : AuditableEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Plate { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int? Year { get; set; }

    /// <summary>Max passenger seats the car physically has (excludes the driver).</summary>
    public int SeatCapacity { get; set; }

    public string? PhotoUrl { get; set; }
    public bool IsDefault { get; set; }
}
