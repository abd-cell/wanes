using NetTopologySuite.Geometries;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Trips;

/// <summary>A driver's planned trip. Can carry several riders (one booking per seat group).</summary>
public class Trip : AuditableEntity
{
    public int DriverId { get; set; }
    public User? Driver { get; set; }

    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    // origin / destination
    public string OriginAddress { get; set; } = string.Empty;
    public Point Origin { get; set; } = default!;         // SRID 4326
    public string DestinationAddress { get; set; } = string.Empty;
    public Point Destination { get; set; } = default!;    // SRID 4326

    /// <summary>Route line for geo matching (may be a straight line in the MVP).</summary>
    public LineString? Route { get; set; }

    public DateTime DepartAt { get; set; }

    /// <summary>Seats offered on this trip; must be &lt;= Vehicle.SeatCapacity.</summary>
    public int SeatsTotal { get; set; }

    /// <summary>Seats still available; 0 =&gt; <see cref="TripStatus.Full"/>.</summary>
    public int SeatsLeft { get; set; }

    /// <summary>Optional display-only price. Not charged (payments out of scope).</summary>
    public decimal? PricePerSeat { get; set; }

    public TripStatus Status { get; set; } = TripStatus.Posted;

    /// <summary>
    /// Concurrency token. Two riders taking the last seat is a genuine race:
    /// both read <see cref="SeatsLeft"/> = 1, both pass the check, and the
    /// second write would sell a seat that no longer exists. A transaction alone
    /// does not stop it — SQL Server reads at READ COMMITTED, so neither
    /// transaction blocks the other's read.
    ///
    /// With this mapped as a row version, EF appends <c>AND RowVersion = @old</c>
    /// to every UPDATE, which turns the read-check-write into one conditional
    /// update: the loser changes no rows and gets a
    /// <c>DbUpdateConcurrencyException</c> instead of overselling. Callers
    /// retry from a fresh read (<c>ConcurrencyRules.MaxAttempts</c>).
    /// </summary>
    public byte[]? RowVersion { get; set; }

    public ICollection<TripStatusHistory> History { get; set; } = [];
}

public class TripStatusHistory : BaseEntity
{
    public int TripId { get; set; }
    public Trip? Trip { get; set; }
    public TripStatus Status { get; set; }
    public int? ChangedBy { get; set; }
}
