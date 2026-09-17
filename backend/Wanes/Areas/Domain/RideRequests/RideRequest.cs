using NetTopologySuite.Geometries;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.RideRequests;

/// <summary>
/// Demand: a journey riders want, that nobody is driving yet.
///
/// **It is not a trip with a null driver.** A <see cref="Trip"/> is
/// transportation that exists, whether or not it has gathered its passengers; a
/// request is transportation that does *not* exist, and the marketplace's job is
/// to make it exist. The two meet at exactly one transition — trip formation,
/// where a selected <see cref="DriverInterest"/> turns this row into a Trip and
/// its <see cref="Participants"/> into that trip's bookings.
///
/// v1 modelled this as a <c>Trips</c> row with three null columns, which bought
/// one real thing — the id never changed — and cost the distinction everywhere
/// else: every query over trips had to remember to exclude it, the demand
/// lifecycle had to borrow trip statuses that did not fit, and interest, offers
/// and pooling had nowhere to live. What it costs the other way is written down
/// honestly: the id **does** change at formation, so anything holding a request
/// id follows <see cref="MatchedTripId"/> to find the ride.
/// </summary>
public class RideRequest : AuditableEntity
{
    // ── The journey ──
    public string OriginAddress { get; set; } = string.Empty;
    public Point Origin { get; set; } = default!;         // SRID 4326
    public string DestinationAddress { get; set; } = string.Empty;
    public Point Destination { get; set; } = default!;    // SRID 4326

    /// <summary>Route line for corridor matching — the same straight line a trip carries in the MVP.</summary>
    public LineString? Route { get; set; }

    /// <summary>The departure the riders want.</summary>
    public DateTime DepartAt { get; set; }

    /// <summary>
    /// How far either side of <see cref="DepartAt"/> is acceptable, in minutes.
    ///
    /// On the request rather than read from configuration at match time because
    /// it is a **rider's** statement about their own day: someone catching a
    /// flight and someone commuting are not equally flexible, and a window that
    /// only ever came from a global setting could not tell them apart. The
    /// configured value seeds it.
    /// </summary>
    public int TimeWindowMinutes { get; set; }

    /// <summary>
    /// Seats wanted, summed over the active participants. Denormalised because
    /// every match query filters on it — a vehicle too small cannot serve the
    /// pool — and a join per candidate row is what that filter would otherwise
    /// cost.
    /// </summary>
    public int SeatsRequested { get; set; }

    // ── The riders' conditions ──
    //
    // The mirror of a driver's (Trip.GenderPolicy et al), pointed the other way.
    // These INTERSECT as the pool grows and never retroactively exclude somebody
    // already on it — see RideRequestService.Join.

    /// <summary>Who the riders will share the car with.</summary>
    public GenderPolicy GenderPolicy { get; set; } = GenderPolicy.Any;

    /// <summary>Who they will be driven by. Checked when a driver expresses interest.</summary>
    public GenderPolicy DriverGenderPolicy { get; set; } = GenderPolicy.Any;

    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    /// <summary>The conditions as one value, for the eligibility rules.</summary>
    public RideConditions Conditions => new(GenderPolicy, MinAge, MaxAge);

    /// <summary>How far the riders asked to be reached from, in metres.</summary>
    public int RadiusMeters { get; set; } = DefaultRadiusMeters;

    private const int DefaultRadiusMeters = 5000;

    /// <summary>
    /// When nearby drivers were last pushed about this. A request needed within
    /// the hour wakes drivers immediately; one for Thursday sits on the board
    /// silently until it comes into range. Stamped so the sweeper pushes once.
    /// </summary>
    public DateTime? NotifiedAt { get; set; }

    // ── Lifecycle ──

    public RideRequestStatus Status { get; set; } = RideRequestStatus.Open;

    /// <summary>
    /// The trip this became. Set exactly once, with
    /// <see cref="RideRequestStatus.Matched"/>, and never cleared.
    ///
    /// This is the thread that survives the id change. A notification, a deep
    /// link or a screen holding a request id follows this to the ride; without
    /// it, formation would strand every reference written before it.
    /// </summary>
    public int? MatchedTripId { get; set; }

    public Trip? MatchedTrip { get; set; }

    /// <summary>
    /// When the first driver said they were willing. It opens the selection
    /// window (<c>AppConfiguration.DriverSelectionWindowMinutes</c>): at zero the
    /// first interest is selected on the spot, which is first-come-first-served
    /// and today's behaviour; above zero, interests accumulate for that long and
    /// the best is chosen deterministically.
    ///
    /// Stamped on the request rather than computed from the interests so the
    /// sweeper can find requests whose window is due with one indexed predicate
    /// instead of a group-by over every interest in the table.
    /// </summary>
    public DateTime? FirstInterestAt { get; set; }

    /// <summary>
    /// When the collected offers are decided — stamped with the first offer
    /// from <see cref="DriverSelectionRules.DecideAt"/>. Equal to
    /// <see cref="FirstInterestAt"/> for instant work, where the first offer
    /// wins on the spot; later for scheduled work, where riders may compare.
    /// </summary>
    public DateTime? DecideAt { get; set; }

    /// <summary>
    /// The matched request this one replaces, when the driver who took that one
    /// cancelled. A terminal request never reopens; its riders are put back on
    /// the market in a new one, and this is the thread between the two.
    /// </summary>
    public int? ReopenedFromRequestId { get; set; }

    /// <summary>
    /// Concurrency token. Two drivers being selected for one request is the race
    /// that would give its riders two cars, and "only one may win" is not true if
    /// it is a read of <see cref="Status"/> followed by a write. Mapped as a row
    /// version, so selection is one conditional update and the loser changes no
    /// rows — see <c>ConcurrencyRules</c>.
    /// </summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>The schedule that generated this occurrence, if any.</summary>
    public int? ScheduleId { get; set; }

    public Schedules.TripSchedule? Schedule { get; set; }

    /// <summary>
    /// The date of the occurrence this row is, in the schedule's own time zone.
    /// With <see cref="ScheduleId"/> it is the materialiser's idempotency key:
    /// running twice, or catching up after a day down, produces one request per
    /// date and not two of Tuesday's.
    /// </summary>
    public DateOnly? OccurrenceDate { get; set; }

    public ICollection<RideRequestParticipant> Participants { get; set; } = [];

    public ICollection<DriverInterest> Interests { get; set; } = [];

    /// <summary>Open, and not yet past its own departure — see <c>ExpireDue</c>.</summary>
    public bool IsOpenAt(DateTime now) => Status == RideRequestStatus.Open && DepartAt > now;
}

/// <summary>
/// One rider's place on a request.
///
/// Deliberately **not** a <see cref="Bookings.Booking"/>: a booking is a seat on
/// a trip, and there is no trip yet. It becomes one exactly once, at formation,
/// which is the only place a participant turns into a passenger.
/// </summary>
public class RideRequestParticipant : AuditableEntity
{
    public int RideRequestId { get; set; }
    public RideRequest? RideRequest { get; set; }

    public int RiderId { get; set; }
    public User? Rider { get; set; }

    public int Seats { get; set; } = 1;

    public RideRequestParticipantStatus Status { get; set; } = RideRequestParticipantStatus.Active;

    // The conditions this rider stated when they joined, kept so the pool's
    // intersection can be explained and so a leaver does not silently loosen it.
    public GenderPolicy CoRiderGenderPolicy { get; set; } = GenderPolicy.Any;
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    /// <summary>When this rider agreed the ride is shared. Null for older clients.</summary>
    public DateTime? SharedTermsAcceptedAt { get; set; }

    public RideConditions Conditions => new(CoRiderGenderPolicy, MinAge, MaxAge);
}

/// <summary>
/// A driver's willingness to serve one request — and, because it carries a
/// vehicle, a price and a message, an **offer** in everything but name.
///
/// That is on purpose. V1 selects the first interest and the marketplace behaves
/// exactly as the old claim did; the day riders pick between competing drivers,
/// or a driver quotes against a pool, the data it needs is already here and no
/// domain has to be redesigned around it.
/// </summary>
public class DriverInterest : AuditableEntity
{
    public int RideRequestId { get; set; }
    public RideRequest? RideRequest { get; set; }

    public int DriverId { get; set; }
    public User? Driver { get; set; }

    /// <summary>The car being offered — part of the offer, not metadata: it is what caps the pool.</summary>
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    /// <summary>The price per seat this driver is offering at. Display-only (no payments).</summary>
    public decimal PricePerSeat { get; set; }

    /// <summary>Optional note to the riders. Nothing reads it but them.</summary>
    public string? Message { get; set; }

    public DriverInterestStatus Status { get; set; } = DriverInterestStatus.Interested;

    /// <summary>
    /// Seats the driver will put on the trip — at least the riders' own, at
    /// most the car's. Null offers the whole car, as before.
    /// </summary>
    public int? SeatsOffered { get; set; }

    /// <summary>
    /// The conditional accept: the trip only runs once this many seats are
    /// held. Null (or no more than the riders already on it) is unconditional.
    /// </summary>
    public int? MinPassengers { get; set; }

    /// <summary>When the driver agreed the trip is shared and its free seats stay on sale.</summary>
    public DateTime? SharedTermsAcceptedAt { get; set; }

    /// <summary>Still in the running.</summary>
    public bool IsLive => Status == DriverInterestStatus.Interested;
}
