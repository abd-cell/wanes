using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Trips.Models;

public class CreateTripInput
{
    public int VehicleId { get; set; }
    public GeoPoint Origin { get; set; } = new();
    public GeoPoint Destination { get; set; } = new();
    public DateTime DepartAt { get; set; }

    /// <summary>Seats offered on this trip. Defaults to the vehicle capacity when 0.</summary>
    public int SeatsTotal { get; set; }
    public decimal? PricePerSeat { get; set; }
}
