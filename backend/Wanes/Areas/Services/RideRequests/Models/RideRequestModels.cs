using System.ComponentModel.DataAnnotations;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.RideRequests.Models;

/// <summary>
/// A ride request as both sides read it: the riders who are on it, and the
/// drivers deciding whether to offer for it.
///
/// One shape for both audiences because they need the same facts — where, when,
/// how many seats, what conditions, what it is worth. The figures that only make
/// sense on a driver's card (<see cref="DistanceKm"/>,
/// <see cref="SuggestedPricePerSeat"/>) are cheap and harmless either way;
/// splitting the model would mean keeping two of everything else in step.
/// </summary>
public class RideRequestRow
{
    public int Id { get; set; }

    /// <summary>
    /// The trip this became, once a driver was selected. **Null while it is
    /// still demand.**
    ///
    /// This is the thread across the one identity change in the system. Anything
    /// holding a request id — a notification, a deep link, a screen that was
    /// open when the driver was chosen — follows this to the ride rather than
    /// finding a dead id.
    /// </summary>
    public int? MatchedTripId { get; set; }

    /// <summary>
    /// Who wrote it — the first rider on it.
    ///
    /// Derived from the participants rather than stored, because the author
    /// holds no privilege the others do not: they can leave, and the request
    /// outlives them while anybody else is still on it. A column would imply an
    /// owner there isn't one of.
    /// </summary>
    public int RiderId { get; set; }

    public string? RiderName { get; set; }

    public string OriginAddress { get; set; } = string.Empty;
    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestinationLat { get; set; }
    public double DestinationLng { get; set; }

    /// <summary>The departure wanted, and how far either side of it is acceptable.</summary>
    public DateTime DepartAt { get; set; }

    public int TimeWindowMinutes { get; set; }

    public int SeatsWanted { get; set; }

    /// <summary>How many riders are on it — one when nobody has joined.</summary>
    public int RiderCount { get; set; }

    /// <summary>The caller's own seats, or 0 when they hold none.</summary>
    public int MySeats { get; set; }

    public GenderPolicy DriverGenderPolicy { get; set; }
    public GenderPolicy CoRiderGenderPolicy { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    public RideRequestStatus Status { get; set; }

    /// <summary>
    /// How many drivers have said they are willing. On the rider's card because
    /// "somebody is interested" is the single most useful thing they can be told
    /// while they wait, and on the driver's because a pool three drivers have
    /// already offered for is a different proposition from an unanswered one.
    /// </summary>
    public int InterestCount { get; set; }

    /// <summary>The caller's own live interest, when they are a driver who offered.</summary>
    public bool IHaveOffered { get; set; }

    /// <summary>Straight-line length of the leg, in kilometres.</summary>
    public double DistanceKm { get; set; }

    /// <summary>
    /// What the platform's rates make a seat worth on this leg.
    ///
    /// A suggestion, never a price: the driver's own figure is what their offer
    /// carries. It is here so the price sheet opens on the number the driver has
    /// been looking at on this card rather than on a blank they must invent.
    /// </summary>
    public decimal SuggestedPricePerSeat { get; set; }

    /// <summary>
    /// When the collected offers are decided. Null before the first offer; in
    /// the future while a scheduled request is still comparing drivers.
    /// </summary>
    public DateTime? DecideAt { get; set; }

    /// <summary>The request this one replaced after its driver cancelled.</summary>
    public int? ReopenedFromRequestId { get; set; }

    public RideRequestRow() { }

    public RideRequestRow(
        RideRequest request,
        IReadOnlyCollection<RideRequestParticipant> participants,
        User? author,
        int callerId,
        double distanceKm,
        decimal suggestedPricePerSeat,
        int interestCount = 0,
        bool callerHasOffered = false)
    {
        if (request == null) return;

        Id = request.Id;
        MatchedTripId = request.MatchedTripId;
        OriginAddress = request.OriginAddress;
        OriginLat = request.Origin.Y;
        OriginLng = request.Origin.X;
        DestinationAddress = request.DestinationAddress;
        DestinationLat = request.Destination.Y;
        DestinationLng = request.Destination.X;
        DepartAt = request.DepartAt;
        TimeWindowMinutes = request.TimeWindowMinutes;

        // Summed from the participants rather than trusted from the column: the
        // two are kept in step by the service, and this is the reader that would
        // show the discrepancy if they ever were not.
        var active = participants
            .Where(p => p.Status == RideRequestParticipantStatus.Active)
            .ToList();
        SeatsWanted = active.Sum(p => p.Seats);
        RiderCount = active.Count;
        MySeats = active.Where(p => p.RiderId == callerId).Sum(p => p.Seats);

        var first = active.OrderBy(p => p.Id).FirstOrDefault();
        RiderId = author?.Id ?? first?.RiderId ?? 0;
        RiderName = author?.DisplayName ?? author?.FirstName;

        DriverGenderPolicy = request.DriverGenderPolicy;
        CoRiderGenderPolicy = request.GenderPolicy;
        MinAge = request.MinAge;
        MaxAge = request.MaxAge;
        Status = request.Status;
        InterestCount = interestCount;
        IHaveOffered = callerHasOffered;
        DistanceKm = Math.Round(distanceKm, 2);
        SuggestedPricePerSeat = suggestedPricePerSeat;
        DecideAt = request.DecideAt;
        ReopenedFromRequestId = request.ReopenedFromRequestId;
    }
}

/// <summary>
/// What a rider says when they create demand: where, when, how many seats, and
/// who they are willing to travel with.
///
/// No price. That is the shape of the exchange — the rider states the need, a
/// driver offers a figure, and the rider's answer to a figure they dislike is to
/// leave.
/// </summary>
public class CreateRideRequestInput
{
    public GeoPoint Origin { get; set; } = new();
    public GeoPoint Destination { get; set; } = new();

    /// <summary>
    /// The departure wanted. Refused if it is sooner than a driver could gather
    /// this many riders and run the leg — see
    /// <see cref="RiderTripRules.EarliestDeparture"/>.
    /// </summary>
    public DateTime DepartAt { get; set; }

    /// <summary>
    /// How far either side of <see cref="DepartAt"/> the rider will accept, in
    /// minutes. Zero takes the marketplace's configured window — most riders
    /// have no opinion, and the ones who do are the reason the field exists.
    /// </summary>
    [Range(0, 240)]
    public int TimeWindowMinutes { get; set; }

    [Range(1, RiderTripRules.MaxSeats)]
    public int Seats { get; set; } = 1;

    /// <summary>How far away a driver may be and still be shown this request.</summary>
    public bool Nearby { get; set; } = true;

    /// <summary>Who may drive it.</summary>
    [EnumDataType(typeof(GenderPolicy))]
    public GenderPolicy DriverGenderPolicy { get; set; } = GenderPolicy.Any;

    /// <summary>Who may join it.</summary>
    [EnumDataType(typeof(GenderPolicy))]
    public GenderPolicy CoRiderGenderPolicy { get; set; } = GenderPolicy.Any;

    [Range(RideAgeBounds.Min, RideAgeBounds.Max)]
    public int? MinAge { get; set; }

    [Range(RideAgeBounds.Min, RideAgeBounds.Max)]
    public int? MaxAge { get; set; }

    public RideConditions CoRiderConditions => new(CoRiderGenderPolicy, MinAge, MaxAge);

    /// <summary>The rider agreed the ride is shared. Recorded on their place; older clients send nothing.</summary>
    public bool? AcceptSharedRide { get; set; }
}

/// <summary>
/// What a second rider says when they join somebody else's request.
///
/// They bring their own conditions, which is what makes joining a two-way
/// check: they must satisfy the pool, and the pool's riders must satisfy them.
/// </summary>
public class JoinRideRequestInput
{
    [Range(1, RiderTripRules.MaxSeats)]
    public int Seats { get; set; } = 1;

    [EnumDataType(typeof(GenderPolicy))]
    public GenderPolicy DriverGenderPolicy { get; set; } = GenderPolicy.Any;

    [EnumDataType(typeof(GenderPolicy))]
    public GenderPolicy CoRiderGenderPolicy { get; set; } = GenderPolicy.Any;

    [Range(RideAgeBounds.Min, RideAgeBounds.Max)]
    public int? MinAge { get; set; }

    [Range(RideAgeBounds.Min, RideAgeBounds.Max)]
    public int? MaxAge { get; set; }

    public RideConditions Conditions => new(CoRiderGenderPolicy, MinAge, MaxAge);

    /// <summary>The rider agreed the ride is shared.</summary>
    public bool? AcceptSharedRide { get; set; }
}

/// <summary>
/// A driver's offer: which car, what a seat costs, and anything they want the
/// riders to know.
///
/// The same shape whether the marketplace selects immediately or holds a window
/// open for competing offers (<see cref="DriverSelectionRules"/>) — which is the
/// point of recording interest rather than taking the trip outright.
/// </summary>
public class ExpressInterestInput
{
    /// <summary>
    /// The car. Optional only so an older client still works — the driver's
    /// default stands in — because seats come from the vehicle and an offer
    /// without one has nothing to seat anybody in.
    /// </summary>
    public int? VehicleId { get; set; }

    /// <summary>
    /// What the driver is charging per seat.
    ///
    /// Nullable so the server still has an answer when the field does not
    /// arrive — an older build, a retried call — and derives it from the
    /// distance (<see cref="FareRules"/>) rather than producing the one kind of
    /// trip that lists with a blank price. Zero is a real answer: a free seat is
    /// a favour, not a missing field.
    /// </summary>
    [Range(typeof(decimal), "0", "1000")]
    public decimal? PricePerSeat { get; set; }

    [MaxLength(300)]
    public string? Message { get; set; }

    /// <summary>
    /// The driver agreed that the trip is shared: the seats they do not fill
    /// from this request stay on sale, and more riders may join until
    /// departure. Required while <c>RequireSharedTermsAcceptance</c> is on.
    /// </summary>
    public bool? AcceptSharedTrip { get; set; }

    /// <summary>
    /// Seats the driver will put on the trip. At least the riders' own, at
    /// most the car's; null offers the whole car.
    /// </summary>
    [Range(1, RiderTripRules.MaxSeats)]
    public int? SeatsOffered { get; set; }

    /// <summary>
    /// The conditional accept — "I'll take it if it reaches this many". Above
    /// the riders already on it, the trip forms gathering and runs only once
    /// the seats are held (or the driver decides to run anyway).
    /// </summary>
    [Range(1, RiderTripRules.MaxSeats)]
    public int? MinPassengers { get; set; }
}
