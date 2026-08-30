namespace Wanes.Shareds.Models;

/// <summary>A geographic coordinate plus its human-readable address, used across DTOs.</summary>
public class GeoPoint
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string Address { get; set; } = string.Empty;
}
