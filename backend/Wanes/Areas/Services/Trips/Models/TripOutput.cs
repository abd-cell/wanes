using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Trips.Models;

public class TripOutput
{
    public int Id { get; set; }
    public int? DriverId { get; set; }
    public string? DriverName { get; set; }
    public double DriverRating { get; set; }

    /// <summary>Trips the driver has completed, and the share of accepted trips they completed (0–1, null when new).</summary>
    public int DriverTrips { get; set; }
    public double? DriverCompletionRate { get; set; }

    public int? VehicleId { get; set; }

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

    // ── The driver's conditions ──

    /// <summary>Seats needed before anybody is confirmed; 1 means no condition.</summary>
    public int MinSeatsToConfirm { get; set; }

    /// <summary>
    /// Seats already held, committed or not. Arithmetic rather than a query:
    /// every held seat is off <see cref="SeatsLeft"/> the moment it is taken.
    /// </summary>
    public int SeatsHeld { get; set; }

    /// <summary>
    /// The trip is short of the seats its driver asked for, so it may yet not
    /// run. Shown on the card — hiding it would let a rider book a trip that
    /// might be called off without knowing that was possible.
    /// </summary>
    public bool IsGathering { get; set; }

    /// <summary>
    /// Its passengers are committed. The other half of <see cref="IsGathering"/>
    /// and not merely its negation: a trip with no threshold at all is neither
    /// gathering nor confirmed until somebody takes a seat on it.
    /// </summary>
    public bool IsConfirmed { get; set; }

    /// <summary>
    /// No seats left. Sent rather than left to the client to work out, so both
    /// apps and the console say "full" on exactly the same rule — it stopped
    /// being a status and a client comparing against the old one would be
    /// quietly wrong.
    /// </summary>
    public bool IsFull { get; set; }

    public GenderPolicy GenderPolicy { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    /// <summary>
    /// When the driver actually started, from the status history. Lets the
    /// rider's map advance the car across the journey against a real clock
    /// instead of parking it at an arbitrary fraction of the route.
    ///
    /// Only filled where the history was loaded (the single-trip GET); null
    /// elsewhere, and null on a trip that has not started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>The driver's recurring schedule this trip is one day of.</summary>
    public int? ScheduleId { get; set; }
    public DateOnly? OccurrenceDate { get; set; }

    /// <summary>The driver's series commitment this trip was formed under (a day of a rider's series).</summary>
    public int? SeriesCommitmentId { get; set; }

    /// <summary>The recurrence behind the trip, filled by the series decorator.</summary>
    public Series.Models.SeriesInfo? Series { get; set; }

    public TripOutput() { }

    public TripOutput(Trip trip, User? driver) : this(trip, driver, trip?.Vehicle) { }

    public TripOutput(Trip trip, User? driver, Vehicle? vehicle)
    {
        if (trip == null) return;

        Id = trip.Id;
        DriverId = trip.DriverId;
        DriverName = driver?.DisplayName ?? driver?.FirstName;
        DriverRating = driver?.RatingAvg ?? 0;
        DriverTrips = driver?.TripsAsDriver ?? 0;
        DriverCompletionRate = driver == null
            ? null
            : Domain.Marketplace.ReliabilityRules.CompletionRate(driver.TripsAsDriver, driver.DriverCancellations);
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
        MinSeatsToConfirm = trip.MinSeatsToConfirm;
        SeatsHeld = trip.SeatsTotal - trip.SeatsLeft;
        IsGathering = TripConfirmationRules.IsGathering(
            trip.Status, trip.MinSeatsToConfirm, SeatsHeld, trip.ConfirmedAt);
        IsConfirmed = trip.IsConfirmed;
        IsFull = trip.IsFull;
        GenderPolicy = trip.GenderPolicy;
        MinAge = trip.MinAge;
        MaxAge = trip.MaxAge;
        ScheduleId = trip.ScheduleId;
        OccurrenceDate = trip.OccurrenceDate;
        SeriesCommitmentId = trip.SeriesCommitmentId;
        StartedAt = trip.History?
            .Where(h => h.Status == TripStatus.Active)
            .OrderByDescending(h => h.Id)
            .FirstOrDefault()?.CreationDate;
    }
}
