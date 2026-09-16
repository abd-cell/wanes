using NetTopologySuite.Geometries;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Schedules;

/// <summary>
/// A recurring posting — the same ride every day, on chosen weekdays, or once a
/// month — owned by whichever side wrote it.
///
/// It is a <b>generator, not a match</b>. A worker materialises it into ordinary
/// rows over a rolling horizon (<see cref="RecurrenceRules.HorizonDays"/>):
/// <see cref="Trip"/>s for a driver's schedule, <c>RiderTrip</c>s for a rider's.
/// Search, booking, claiming, ranking and the driver's availability rules never
/// learn that recurrence exists — which is the point. A second matchable row
/// type is how "one trip type" quietly becomes two sets of rules to keep in
/// step.
///
/// Everything a generated row needs therefore lives here, and the fields that
/// only make sense for one side are nullable rather than split across two
/// entities: the two schedules differ in three columns, and a second table
/// would duplicate the recurrence, the route, the horizon and the worker.
/// </summary>
public class TripSchedule : AuditableEntity
{
    public int OwnerId { get; set; }
    public User? Owner { get; set; }

    /// <summary>
    /// Which side wrote it, and so which kind of row it generates. Not derived
    /// from the owner's <c>ActiveRole</c>: that is a UI mode the user flips, and
    /// a driver browsing as a rider must not silently turn their commute
    /// schedule into demand.
    /// </summary>
    public ActiveRole OwnerRole { get; set; } = ActiveRole.Rider;

    public string OriginAddress { get; set; } = string.Empty;
    public Point Origin { get; set; } = default!;         // SRID 4326
    public string DestinationAddress { get; set; } = string.Empty;
    public Point Destination { get; set; } = default!;    // SRID 4326

    public Recurrence Recurrence { get; set; } = Recurrence.Weekly;

    /// <summary><see cref="Recurrence.Weekly"/> only: the days it runs on.</summary>
    public WeekDays DaysOfWeek { get; set; } = WeekDays.None;

    /// <summary>
    /// <see cref="Recurrence.Monthly"/> only: 1–31, clamped to the length of
    /// each month so the 31st still runs in February.
    /// </summary>
    public int? DayOfMonth { get; set; }

    /// <summary>
    /// The wall-clock departure, in <see cref="TimeZoneId"/>. Stored as local
    /// time because that is what people mean — "the seven o'clock" stays the
    /// seven o'clock across a daylight-saving shift, which a stored UTC instant
    /// would not.
    /// </summary>
    public TimeOnly TimeOfDay { get; set; }

    /// <summary>
    /// The zone <see cref="TimeOfDay"/> is read in, as a time-zone id
    /// ("Asia/Amman"). Per schedule rather than per platform: the same instance
    /// can serve two countries, and a rider who moves keeps their old commute
    /// on the old clock until they edit it.
    /// </summary>
    public string? TimeZoneId { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>Open-ended when null — it runs until paused or deleted.</summary>
    public DateOnly? EndDate { get; set; }

    public int Seats { get; set; } = 1;

    // ── Driver-owned schedules only ──

    /// <summary>The price each generated trip is listed at, per seat.</summary>
    public decimal? PricePerSeat { get; set; }

    public int? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    /// <summary>Seats that must be held before a generated trip confirms.</summary>
    public int MinSeatsToConfirm { get; set; } = TripConfirmationRules.NoThreshold;

    // ── Conditions ──
    //
    // Read as the driver's conditions on riders for a driver-owned schedule, and
    // as the riders' conditions on their driver and co-riders for a rider-owned
    // one. One set of columns, because a schedule only ever generates one kind
    // of row and the conditions travel onto it unchanged.

    public GenderPolicy GenderPolicy { get; set; } = GenderPolicy.Any;

    /// <summary>
    /// Rider-owned schedules only: who else may be aboard, as distinct from
    /// <see cref="GenderPolicy"/>, which on that side says who may drive.
    ///
    /// A driver-owned schedule leaves it <see cref="GenderPolicy.Any"/> and
    /// unread — a driver sets one condition, on their passengers, and that is
    /// what <see cref="GenderPolicy"/> holds for them. The column exists
    /// because a rider's posting genuinely carries two, and the generated row
    /// has to be able to say both. It used to read the owner's account
    /// preference for this, which is gone: the condition now travels with the
    /// thing it applies to.
    /// </summary>
    public GenderPolicy CoRiderGenderPolicy { get; set; } = GenderPolicy.Any;

    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    /// <summary>
    /// Generation is stopped, and what has already been generated stands.
    ///
    /// Distinct from deletion: pausing a commute for a fortnight's holiday
    /// should not cancel the trips people have already booked onto, and should
    /// not require rebuilding the schedule afterwards.
    /// </summary>
    public bool IsPaused { get; set; }

    /// <summary>
    /// The last occurrence date the materialiser has considered. Advanced only
    /// forwards, so a pass that runs twice does not reconsider the same
    /// mornings — the unique index on (schedule, occurrence date) is the real
    /// guarantee, and this keeps the ordinary pass cheap.
    /// </summary>
    public DateOnly? MaterialisedThrough { get; set; }

    /// <summary>The conditions as one value, for the eligibility rules.</summary>
    public RideConditions Conditions => new(GenderPolicy, MinAge, MaxAge);
}
