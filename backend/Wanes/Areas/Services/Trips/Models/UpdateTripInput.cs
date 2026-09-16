using System.ComponentModel.DataAnnotations;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

using Wanes.Areas.Domain.Trips;

namespace Wanes.Areas.Services.Trips.Models;

/// <summary>
/// Edits a trip the driver already published. Only allowed while the trip is
/// still <see cref="Shareds.Enums.TripStatus.Posted"/> and nobody has booked it.
/// </summary>
public class UpdateTripInput
{
    public int VehicleId { get; set; }
    public GeoPoint Origin { get; set; } = new();
    public GeoPoint Destination { get; set; } = new();
    public DateTime DepartAt { get; set; }

    /// <summary>Seats offered on this trip. Defaults to the vehicle capacity when 0.</summary>
    public int SeatsTotal { get; set; }
    public decimal? PricePerSeat { get; set; }

    // ── The driver's conditions ──

    /// <summary>
    /// Seats that must be held before anybody is confirmed. 0 or 1 means no
    /// condition: the first seat commits, as an ordinary trip does.
    /// </summary>
    [Range(0, RiderTripRules.MaxSeats)]
    public int MinSeatsToConfirm { get; set; }

    /// <summary>Who may take a seat.</summary>
    [EnumDataType(typeof(GenderPolicy))]
    public GenderPolicy GenderPolicy { get; set; } = GenderPolicy.Any;

    [Range(RideAgeBounds.Min, RideAgeBounds.Max)]
    public int? MinAge { get; set; }

    [Range(RideAgeBounds.Min, RideAgeBounds.Max)]
    public int? MaxAge { get; set; }
}
