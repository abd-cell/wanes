using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Bookings;

/// <summary>
/// Links a rider to a trip, reserving one or more seats.
///
/// One shape covers both ways a rider gets aboard, because there is only one
/// kind of trip. A seat taken from search on a trip somebody is already driving,
/// and a seat on a trip a rider wrote themselves and nobody is driving yet, are
/// the same row — the second one simply sits on a trip whose driver is still
/// null. There is no separate "hold": holding a seat on a trip you posted *is*
/// booking it.
/// </summary>
public class Booking : AuditableEntity
{
    public int TripId { get; set; }
    public Trip? Trip { get; set; }

    public int RiderId { get; set; }
    public User? Rider { get; set; }

    public int Seats { get; set; } = 1;

    /// <summary>
    /// Where the seat stands. <see cref="BookingStatus.Pending"/> means the seat
    /// is held but nobody is committed yet, and it has exactly two producers:
    ///
    /// - a trip with **no driver yet** — nobody has agreed to carry this rider,
    ///   so there is nothing to be confirmed against;
    /// - a trip short of the seats its driver asked for
    ///   (<see cref="TripConfirmationRules"/>).
    ///
    /// Both mean the same thing to the rider — the seat is theirs but the trip
    /// might not run — and both resolve without them doing anything. A driver
    /// taking the trip confirms the first; the threshold being met confirms the
    /// second.
    /// </summary>
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    // ── The rider's own conditions, as they were when they took the seat ──
    //
    // Only meaningful while the trip has no driver, where the riders' conditions
    // intersect as each one joins and the trip carries the strictest of them.
    // Copied onto the booking rather than read back off the User: a rider who
    // changes their profile next month must not retroactively invalidate a trip
    // they are already on, and a joiner has to be checked against what the
    // people already aboard actually agreed to.

    public GenderPolicy CoRiderGenderPolicy { get; set; } = GenderPolicy.Any;

    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    // ── Safety ──

    /// <summary>
    /// Four digits the rider reads to the driver at pickup. The driver cannot
    /// mark the seat boarded without it (when the setting is on), which is how
    /// both know the right person got into the right car.
    /// </summary>
    public string? BoardingCode { get; set; }

    /// <summary>
    /// The handle behind a read-only "follow my trip" link the rider shared.
    /// Null until they share; cleared when they stop.
    /// </summary>
    public string? ShareToken { get; set; }

    public DateTime? ShareTokenCreatedAt { get; set; }

    /// <summary>When the rider agreed the ride is shared. Null for older clients.</summary>
    public DateTime? SharedTermsAcceptedAt { get; set; }

    /// <summary>
    /// The rider's series booking this seat was made under, if any. Decides the
    /// notice a cancellation needs (<c>SeriesSkipNoticeHours</c>) and which
    /// seats ending the series gives back.
    /// </summary>
    public int? SeriesCommitmentId { get; set; }

    /// <summary>What this rider requires of the others, as one value.</summary>
    public RideConditions Conditions => new(CoRiderGenderPolicy, MinAge, MaxAge);
}
