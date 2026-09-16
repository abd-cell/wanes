using NetTopologySuite.Geometries;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Trips;

/// <summary>
/// A planned trip. Can carry several riders (one booking per seat group).
///
/// **A trip always has a driver.** It is transportation that exists, whether or
/// not it has gathered its passengers yet. Two routes lead here and neither is
/// recorded: a driver publishing seats, and a <c>RideRequest</c> that found one
/// (see <c>TripFormationService</c>). After formation the two are the same
/// thing — one search index, one booking rule, one ranking — which is why there
/// is deliberately no <c>Source</c> or <c>OfferedBy</c> column. Nothing
/// downstream can ask how a trip came about, so nothing can start behaving
/// differently for one of them.
///
/// <see cref="DriverId"/>, <see cref="VehicleId"/> and
/// <see cref="PricePerSeat"/> remain nullable only because the columns predate
/// v2 and old rows carry nulls; every trip written now has all three.
/// </summary>
public class Trip : AuditableEntity
{
    /// <summary>
    /// Who is driving it. **Null until somebody takes it** — a rider's posting
    /// has no driver, which is the whole reason it is on the board.
    /// </summary>
    public int? DriverId { get; set; }

    public User? Driver { get; set; }

    /// <summary>The car. Null for the same reason as <see cref="DriverId"/>.</summary>
    public int? VehicleId { get; set; }

    public Vehicle? Vehicle { get; set; }

    /// <summary>Somebody is driving this. The one test worth naming.</summary>
    public bool HasDriver => DriverId != null;

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

    /// <summary>Seats still available.</summary>
    public int SeatsLeft { get; set; }

    /// <summary>
    /// No seats left. **A capacity condition, not a status** — the trip still
    /// runs, still confirms and still completes; it simply accepts nobody else
    /// and drops out of search.
    ///
    /// This used to be <c>TripStatus.Full</c>, which put capacity in the same
    /// column as "is the car moving" and meant every lifecycle check had to
    /// spell <c>is Posted or Full</c>. Capacity is the one dimension that moves
    /// both ways — a cancelled seat unfills a trip — so it cannot live in a
    /// ladder. Derived, never stored: <see cref="SeatsLeft"/> is the only truth.
    /// </summary>
    public bool IsFull => SeatsLeft <= 0;

    /// <summary>
    /// Display-only price per seat. Not charged (payments out of scope).
    ///
    /// Null on a trip nobody is driving yet: the price is the driver's to name,
    /// and naming it is part of taking the trip. A rider who does not like the
    /// figure leaves, which costs them nothing.
    /// </summary>
    public decimal? PricePerSeat { get; set; }

    public TripStatus Status { get; set; } = TripStatus.Posted;

    // ── The driver's conditions ──
    //
    // What the driver drives under: how many seats make the run worth making,
    // and who may take them. Checked through TripConfirmationRules and
    // RiderEligibilityRules — never inline, because search filters on the same
    // questions that booking refuses on.

    /// <summary>
    /// Seats that must be held before anybody is confirmed.
    /// <see cref="TripConfirmationRules.NoThreshold"/> means no condition.
    /// </summary>
    public int MinSeatsToConfirm { get; set; } = TripConfirmationRules.NoThreshold;

    /// <summary>Who may take a seat.</summary>
    public GenderPolicy GenderPolicy { get; set; } = GenderPolicy.Any;

    /// <summary>
    /// Who may drive it — the riders' condition on their driver, and the mirror
    /// of <see cref="GenderPolicy"/>.
    ///
    /// Meaningful only while <see cref="DriverId"/> is null: it is checked when
    /// somebody takes the trip and is dead weight afterwards. It lives here
    /// rather than on the riders because it is a property of the trip they are
    /// sharing — the strictest requirement among everybody aboard, tightened as
    /// each one joins.
    /// </summary>
    public GenderPolicy DriverGenderPolicy { get; set; } = GenderPolicy.Any;

    /// <summary>
    /// How far the riders asked to be reached from, in metres. Read by the
    /// driver's board so the list agrees with the push: a rider who opted into a
    /// wide match is findable by a driver whose own filter is narrower.
    /// </summary>
    public int RadiusMeters { get; set; } = DefaultRadiusMeters;

    private const int DefaultRadiusMeters = 5000;

    /// <summary>
    /// When nearby drivers were last pushed about a trip with no driver.
    ///
    /// It is on the board the moment it is written, but a trip needed on
    /// Thursday does not push until it comes within
    /// <c>RiderTripRules.NotifyLead</c> of departure — a ride needed in twenty
    /// minutes has to wake drivers up, a ride needed on Thursday must not ping
    /// every driver in town on Monday. Stamped so the sweeper pushes once and
    /// not on every tick.
    /// </summary>
    public DateTime? NotifiedAt { get; set; }

    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    /// <summary>The conditions as one value, for the eligibility rules.</summary>
    public RideConditions Conditions => new(GenderPolicy, MinAge, MaxAge);

    /// <summary>
    /// When the driver was asked to run-or-cancel a trip short of its threshold.
    ///
    /// Stamped so the sweeper asks once: without it every tick between the
    /// prompt and the cutoff would push the same question again, and a driver
    /// buzzed four times in a quarter of an hour learns to ignore the one
    /// notification that needed an answer.
    /// </summary>
    public DateTime? ConfirmPromptedAt { get; set; }

    /// <summary>
    /// When this trip's passengers became committed — null while it is still
    /// gathering. Confirmation is its **own dimension**, beside the lifecycle
    /// and the capacity, and this is the whole of it.
    ///
    /// It is **stored, not derived**, and that is the point. Derived from the
    /// held seats it oscillated: a trip that reached its threshold, told three
    /// riders they were confirmed, and then lost a seat would quietly go back to
    /// gathering and tell them the opposite. Once a rider has been told their
    /// ride is on, a stranger changing their mind must not take it away.
    ///
    /// Set **once**, inside the same commit that confirms the bookings, which is
    /// also what makes a retried confirm free: a trip that is already stamped
    /// confirms nobody again and notifies nobody again.
    /// </summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>Its passengers are committed.</summary>
    public bool IsConfirmed => ConfirmedAt != null;

    /// <summary>The schedule that generated this trip, if any.</summary>
    public int? ScheduleId { get; set; }

    public Schedules.TripSchedule? Schedule { get; set; }

    /// <summary>
    /// The date of the occurrence this row is, in the schedule's own time zone.
    /// With <see cref="ScheduleId"/> it is the materialiser's idempotency key.
    ///
    /// Note what this is *not*: a marker for how the trip came about. A trip
    /// claimed from a rider's posting carries nothing at all — no
    /// <c>Source</c>, no <c>OfferedBy</c> — so nothing downstream can start
    /// behaving differently for one route into a trip. This says which
    /// *generator* wrote the row, which is a different question, and it exists
    /// because deleting a series has to be able to find its own future rows.
    /// </summary>
    public DateOnly? OccurrenceDate { get; set; }

    /// <summary>
    /// Concurrency token. Two riders taking the last seat is a genuine race,
    /// and so is two drivers taking the same driverless trip — "first to take it
    /// wins" is not a claim if it is a read of <see cref="DriverId"/> followed by
    /// a write. Both races are closed by the same version.
    ///
    /// On the seat race:
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
