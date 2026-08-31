using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Trips.Models;

public class TripOutput
{
    public int Id { get; set; }
    public int DriverId { get; set; }
    public string? DriverName { get; set; }
    public double DriverRating { get; set; }
    public int VehicleId { get; set; }

    /// <summary>"Toyota Prius" — shown to the rider on results/booking screens.</summary>
    public string? VehicleLabel { get; set; }
    public string? VehicleColor { get; set; }
    public string? VehiclePlate { get; set; }

    public string OriginAddress { get; set; } = string.Empty;
    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestinationLat { get; set; }
    public double DestinationLng { get; set; }
    public DateTime DepartAt { get; set; }
    public int SeatsTotal { get; set; }
    public int SeatsLeft { get; set; }
    public decimal? PricePerSeat { get; set; }
    public TripStatus Status { get; set; }

    /// <summary>
    /// When the driver actually started, from the status history. Lets the
    /// rider's map advance the car across the journey against a real clock
    /// instead of parking it at an arbitrary fraction of the route.
    ///
    /// Only filled where the history was loaded (the single-trip GET); null
    /// elsewhere, and null on a trip that has not started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    public TripOutput() { }

    public TripOutput(Trip trip, User? driver) : this(trip, driver, trip?.Vehicle) { }

    public TripOutput(Trip trip, User? driver, Vehicle? vehicle)
    {
        if (trip == null) return;

        Id = trip.Id;
        DriverId = trip.DriverId;
        DriverName = driver?.DisplayName ?? driver?.FirstName;
        DriverRating = driver?.RatingAvg ?? 0;
        VehicleId = trip.VehicleId;
        if (vehicle != null)
        {
            VehicleLabel = $"{vehicle.Make} {vehicle.Model}".Trim();
            VehicleColor = vehicle.Color;
            VehiclePlate = vehicle.Plate;
        }
        OriginAddress = trip.OriginAddress;
        OriginLat = trip.Origin.Y;
        OriginLng = trip.Origin.X;
        DestinationAddress = trip.DestinationAddress;
        DestinationLat = trip.Destination.Y;
        DestinationLng = trip.Destination.X;
        DepartAt = trip.DepartAt;
        SeatsTotal = trip.SeatsTotal;
        SeatsLeft = trip.SeatsLeft;
        PricePerSeat = trip.PricePerSeat;
        Status = trip.Status;
        StartedAt = trip.History?
            .Where(h => h.Status == TripStatus.Active)
            .OrderByDescending(h => h.Id)
            .FirstOrDefault()?.CreationDate;
    }
}
