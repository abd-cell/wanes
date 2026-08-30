using Wanes.Areas.Domain.Trips;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>Admin view of a trip (list + detail share one shape).</summary>
public class TripRow
{
    public int Id { get; set; }
    public int DriverId { get; set; }
    public string? DriverName { get; set; }
    public int VehicleId { get; set; }
    public string? VehicleLabel { get; set; }
    public string OriginAddress { get; set; } = string.Empty;
    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestLat { get; set; }
    public double DestLng { get; set; }
    public DateTime DepartAt { get; set; }
    public int SeatsTotal { get; set; }
    public int SeatsLeft { get; set; }
    public decimal? PricePerSeat { get; set; }
    public TripStatus Status { get; set; }
    public DateTime CreationDate { get; set; }

    public TripRow() { }

    public TripRow(Trip e)
    {
        Id = e.Id;
        DriverId = e.DriverId;
        VehicleId = e.VehicleId;
        OriginAddress = e.OriginAddress;
        OriginLat = e.Origin.Y;
        OriginLng = e.Origin.X;
        DestinationAddress = e.DestinationAddress;
        DestLat = e.Destination.Y;
        DestLng = e.Destination.X;
        DepartAt = e.DepartAt;
        SeatsTotal = e.SeatsTotal;
        SeatsLeft = e.SeatsLeft;
        PricePerSeat = e.PricePerSeat;
        Status = e.Status;
        CreationDate = e.CreationDate;
    }
}

/// <summary>Create/update payload for a trip.</summary>
public class TripInput
{
    public int DriverId { get; set; }
    public int VehicleId { get; set; }
    public string OriginAddress { get; set; } = string.Empty;
    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestLat { get; set; }
    public double DestLng { get; set; }
    public DateTime DepartAt { get; set; }
    public int SeatsTotal { get; set; }
    public int SeatsLeft { get; set; }
    public decimal? PricePerSeat { get; set; }
    public TripStatus Status { get; set; } = TripStatus.Posted;
}
