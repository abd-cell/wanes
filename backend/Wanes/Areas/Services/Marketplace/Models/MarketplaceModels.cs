using System.ComponentModel.DataAnnotations;
using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Marketplace.Models;

// ── Acknowledgements ─────────────────────────────────────────────────────────

public class AcknowledgementInput
{
    [EnumDataType(typeof(AcknowledgementKind))]
    public AcknowledgementKind Kind { get; set; }

    [Range(1, 1000)]
    public int Version { get; set; } = 1;
}

public class AcknowledgementOutput
{
    public AcknowledgementKind Kind { get; set; }
    public int Version { get; set; }
    public DateTime AcceptedAt { get; set; }

    public AcknowledgementOutput() { }

    public AcknowledgementOutput(UserAcknowledgement e)
    {
        Kind = e.Kind;
        Version = e.Version;
        AcceptedAt = e.AcceptedAt;
    }
}

// ── Demand alerts ────────────────────────────────────────────────────────────

/// <summary>
/// A route to watch, or — with <see cref="RideRequestId"/> — one request to
/// watch. For a request watch the route fields are ignored and copied from it.
/// </summary>
public class DemandAlertInput
{
    public GeoPoint? Origin { get; set; }
    public GeoPoint? Destination { get; set; }

    [Range(500, 50_000)]
    public int RadiusMeters { get; set; } = 3000;

    [Range(1, RiderTripRules.MaxSeats)]
    public int MinSeats { get; set; } = 3;

    public int? RideRequestId { get; set; }

    /// <summary>A route alert that fires only for recurring requests.</summary>
    public bool RecurringOnly { get; set; }
}

public class DemandAlertOutput
{
    public int Id { get; set; }
    public int DriverId { get; set; }
    public string? DriverName { get; set; }
    public string OriginAddress { get; set; } = string.Empty;
    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestinationLat { get; set; }
    public double DestinationLng { get; set; }
    public int RadiusMeters { get; set; }
    public int MinSeats { get; set; }
    public int? RideRequestId { get; set; }
    public bool RecurringOnly { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastNotifiedAt { get; set; }
    public int NotifiedCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public DemandAlertOutput() { }

    public DemandAlertOutput(DemandAlert a)
    {
        Id = a.Id;
        DriverId = a.DriverId;
        DriverName = a.Driver == null ? null : $"{a.Driver.FirstName} {a.Driver.LastName}".Trim();
        OriginAddress = a.OriginAddress;
        OriginLat = a.Origin.Y;
        OriginLng = a.Origin.X;
        DestinationAddress = a.DestinationAddress;
        DestinationLat = a.Destination.Y;
        DestinationLng = a.Destination.X;
        RadiusMeters = a.RadiusMeters;
        MinSeats = a.MinSeats;
        RideRequestId = a.RideRequestId;
        RecurringOnly = a.RecurringOnly;
        IsActive = a.IsActive;
        LastNotifiedAt = a.LastNotifiedAt;
        NotifiedCount = a.NotifiedCount;
        CreatedAt = a.CreationDate;
    }
}

// ── Reliability ──────────────────────────────────────────────────────────────

public class CancelTripInput
{
    [EnumDataType(typeof(CancelReason))]
    public CancelReason? Reason { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

/// <summary>What cancelling would cost, shown before the driver confirms.</summary>
public class CancelPreviewOutput
{
    public ReliabilityEventKind Kind { get; set; }
    public int Points { get; set; }
    public int RidersAffected { get; set; }
    public bool ReasonRequired { get; set; }

    /// <summary>Points in the current window after this one, against the thresholds.</summary>
    public int PointsAfter { get; set; }
    public int WarnPoints { get; set; }
    public int SuspendPoints { get; set; }
    public int WindowDays { get; set; }
    public bool WouldSuspend { get; set; }

    /// <summary>The riders will be put back on the market in a new request.</summary>
    public bool RidersRequeued { get; set; }
}

public class ReliabilityEventRow
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserPhone { get; set; }
    public ActiveRole Role { get; set; }
    public ReliabilityEventKind Kind { get; set; }
    public int Points { get; set; }
    public int? TripId { get; set; }
    public int? BookingId { get; set; }
    public CancelReason? Reason { get; set; }
    public string? Note { get; set; }
    public int RidersAffected { get; set; }
    public int MinutesBeforeDeparture { get; set; }
    public bool NeedsReview { get; set; }
    public bool IsWaived { get; set; }
    public DateTime? WaivedAt { get; set; }
    public string? WaiveNote { get; set; }
    public DateTime CreatedAt { get; set; }

    public ReliabilityEventRow() { }

    public ReliabilityEventRow(ReliabilityEvent e)
    {
        Id = e.Id;
        UserId = e.UserId;
        UserName = e.User == null ? null : $"{e.User.FirstName} {e.User.LastName}".Trim();
        UserPhone = e.User?.Phone;
        Role = e.Role;
        Kind = e.Kind;
        Points = e.Points;
        TripId = e.TripId;
        BookingId = e.BookingId;
        Reason = e.Reason;
        Note = e.Note;
        RidersAffected = e.RidersAffected;
        MinutesBeforeDeparture = e.MinutesBeforeDeparture;
        NeedsReview = e.NeedsReview;
        IsWaived = e.IsWaived;
        WaivedAt = e.WaivedAt;
        WaiveNote = e.WaiveNote;
        CreatedAt = e.CreationDate;
    }
}

/// <summary>A user's own record: what counts now, and what it means.</summary>
public class ReliabilityOutput
{
    public int PointsInWindow { get; set; }
    public int WindowDays { get; set; }
    public int WarnPoints { get; set; }
    public int SuspendPoints { get; set; }
    public DateTime? SuspendedUntil { get; set; }
    public int TripsAsDriver { get; set; }
    public int DriverCancellations { get; set; }
    public double? CompletionRate { get; set; }
    public int RiderLateCancels { get; set; }
    public int RiderNoShows { get; set; }
    public List<ReliabilityEventRow> Recent { get; set; } = [];
}

public class WaiveInput
{
    [StringLength(500)]
    public string? Note { get; set; }

    /// <summary>The same note, under the name the console's edit form uses.</summary>
    [StringLength(500)]
    public string? WaiveNote { get; set; }
}

// ── Offers (riders choosing) ─────────────────────────────────────────────────

/// <summary>One driver's offer on a request, as its riders compare them.</summary>
public class OfferRow
{
    public int InterestId { get; set; }
    public int DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public double DriverRating { get; set; }
    public int DriverRatingCount { get; set; }
    public int DriverTrips { get; set; }
    public double? DriverCompletionRate { get; set; }
    public bool DriverVerified { get; set; }
    public string? VehicleLabel { get; set; }
    public string? VehicleColor { get; set; }
    public int VehicleSeats { get; set; }
    public decimal PricePerSeat { get; set; }
    public int? SeatsOffered { get; set; }
    public int? MinPassengers { get; set; }
    public string? Message { get; set; }
    public DateTime OfferedAt { get; set; }

    public OfferRow() { }

    public OfferRow(DriverInterest i)
    {
        InterestId = i.Id;
        DriverId = i.DriverId;
        DriverName = i.Driver?.DisplayName ?? i.Driver?.FirstName ?? string.Empty;
        DriverRating = i.Driver?.RatingAvg ?? 0;
        DriverRatingCount = i.Driver?.RatingCount ?? 0;
        DriverTrips = i.Driver?.TripsAsDriver ?? 0;
        DriverCompletionRate = i.Driver == null
            ? null
            : ReliabilityRules.CompletionRate(i.Driver.TripsAsDriver, i.Driver.DriverCancellations);
        DriverVerified = i.Driver?.DriverStatus == DriverStatus.Verified;
        if (i.Vehicle != null)
        {
            VehicleLabel = $"{i.Vehicle.Make} {i.Vehicle.Model}".Trim();
            VehicleColor = i.Vehicle.Color;
            VehicleSeats = i.Vehicle.SeatCapacity;
        }
        PricePerSeat = i.PricePerSeat;
        SeatsOffered = i.SeatsOffered;
        MinPassengers = i.MinPassengers;
        Message = i.Message;
        OfferedAt = i.CreationDate;
    }
}

// ── Admin: ride requests ─────────────────────────────────────────────────────

public class RideRequestAdminRow
{
    public int Id { get; set; }
    public string OriginAddress { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public DateTime DepartAt { get; set; }
    public int SeatsRequested { get; set; }
    public int RiderCount { get; set; }
    public string? AuthorName { get; set; }
    public RideRequestStatus Status { get; set; }
    public GenderPolicy DriverGenderPolicy { get; set; }
    public GenderPolicy CoRiderGenderPolicy { get; set; }
    public int InterestCount { get; set; }
    public DateTime? FirstInterestAt { get; set; }
    public DateTime? DecideAt { get; set; }
    public DateTime? NotifiedAt { get; set; }
    public int? MatchedTripId { get; set; }
    public int? ReopenedFromRequestId { get; set; }
    public int? ScheduleId { get; set; }
    public DateTime CreatedAt { get; set; }
}
