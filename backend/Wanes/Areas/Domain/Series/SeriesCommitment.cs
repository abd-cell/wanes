using Wanes.Areas.Domain.Schedules;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Series;

/// <summary>
/// Somebody committing to a whole recurring schedule rather than to one day
/// of it.
///
/// Two shapes, one row:
/// <list type="bullet">
/// <item><see cref="SeriesSide.DriverServes"/> — a driver drives a rider's
/// recurring request. Starts <see cref="SeriesStatus.Proposed"/>; the rider
/// accepts (or the clock does, at <see cref="DecideAt"/>).</item>
/// <item><see cref="SeriesSide.RiderJoins"/> — a rider takes a seat on every
/// day of a driver's recurring trip. Active at once, like any booking.</item>
/// </list>
///
/// The schedule stays a generator (§11.1) and every day stays its own trip
/// with its own bookings (§11.2). A commitment is what the materialiser reads
/// when it writes a day: this driver takes it, these riders get a seat. It
/// never stands in for a booking, and nothing in search or confirmation reads
/// it.
/// </summary>
public class SeriesCommitment : AuditableEntity
{
    public int ScheduleId { get; set; }
    public TripSchedule? Schedule { get; set; }

    public SeriesSide Side { get; set; }
    public SeriesStatus Status { get; set; } = SeriesStatus.Proposed;

    /// <summary>The driver: the one committing (DriverServes) or the schedule owner (RiderJoins).</summary>
    public int DriverId { get; set; }
    public User? Driver { get; set; }

    /// <summary>The rider: the schedule owner (DriverServes) or the one committing (RiderJoins).</summary>
    public int RiderId { get; set; }
    public User? Rider { get; set; }

    /// <summary>DriverServes: the car each day's trip runs in.</summary>
    public int? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    /// <summary>DriverServes: the driver's price per seat, per day.</summary>
    public decimal? PricePerSeat { get; set; }

    /// <summary>
    /// DriverServes: seats the driver puts on each day's trip (null = the car).
    /// RiderJoins: seats the rider books each day.
    /// </summary>
    public int? Seats { get; set; }

    /// <summary>
    /// The days covered, as a subset of the schedule's. None means every day
    /// the schedule runs — which is also what a daily or monthly schedule
    /// always means.
    /// </summary>
    public WeekDays DaysOfWeek { get; set; } = WeekDays.None;

    /// <summary>The last date covered. Null runs as long as the schedule does.</summary>
    public DateOnly? Until { get; set; }

    public string? Message { get; set; }

    /// <summary>A proposal the rider has not answered is decided at this moment.</summary>
    public DateTime? DecideAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    /// <summary>When somebody asked for it to end — immediately, or after the notice period.</summary>
    public DateTime? EndRequestedAt { get; set; }
    public int? EndedBy { get; set; }
    public DateTime? EndedAt { get; set; }
    public CancelReason? EndReason { get; set; }
    public string? EndNote { get; set; }

    public DateTime? SharedTermsAcceptedAt { get; set; }

    /// <summary>The last weekly summary sent for it.</summary>
    public DateTime? LastSummaryAt { get; set; }

    /// <summary>Whether the commitment covers this occurrence date.</summary>
    public bool Covers(DateOnly date) =>
        Status == SeriesStatus.Active
        && (Until == null || date <= Until)
        && (DaysOfWeek == WeekDays.None || RecurrenceRules.Includes(DaysOfWeek, date));

    /// <summary>The user on the committing side.</summary>
    public int CommitterId => Side == SeriesSide.DriverServes ? DriverId : RiderId;
}
