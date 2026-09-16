using System.ComponentModel.DataAnnotations;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.Schedules;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

using Wanes.Areas.Domain.Trips;

namespace Wanes.Areas.Services.Schedules.Models;

/// <summary>
/// A recurrence, as either side writes it. The three driver-only fields are
/// nullable and ignored on a rider's schedule — see
/// <see cref="TripSchedule"/> for why one shape serves both.
/// </summary>
public class TripScheduleInput
{
    /// <summary>
    /// Which side is writing it, and so what the schedule generates. Sent
    /// explicitly rather than read off the caller's active role: that is a UI
    /// mode people flip, and a driver browsing as a rider must not silently turn
    /// their commute into demand.
    /// </summary>
    [EnumDataType(typeof(ActiveRole))]
    public ActiveRole OwnerRole { get; set; } = ActiveRole.Rider;

    public GeoPoint Origin { get; set; } = new();
    public GeoPoint Destination { get; set; } = new();

    [EnumDataType(typeof(Recurrence))]
    public Recurrence Recurrence { get; set; } = Recurrence.Weekly;

    /// <summary>Weekly only. Ignored — and not required — for the other two.</summary>
    public WeekDays DaysOfWeek { get; set; } = WeekDays.None;

    /// <summary>Monthly only, 1–31. The 31st runs on the last day of shorter months.</summary>
    [Range(1, 31)]
    public int? DayOfMonth { get; set; }

    /// <summary>Local wall-clock departure, read in <see cref="TimeZoneId"/>.</summary>
    public TimeOnly TimeOfDay { get; set; }

    /// <summary>
    /// A time-zone id ("Asia/Amman"). Optional: an id this host does not know,
    /// or none at all, falls back to UTC rather than refusing the schedule.
    /// </summary>
    [StringLength(64)]
    public string? TimeZoneId { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>Open-ended when null.</summary>
    public DateOnly? EndDate { get; set; }

    [Range(1, RiderTripRules.MaxSeats)]
    public int Seats { get; set; } = 1;

    // ── Driver-owned schedules ──

    [Range(typeof(decimal), "0", "1000")]
    public decimal? PricePerSeat { get; set; }

    public int? VehicleId { get; set; }

    [Range(0, RiderTripRules.MaxSeats)]
    public int MinSeatsToConfirm { get; set; }

    // ── Conditions ──
    //
    // The driver's on their riders, or the riders' on their driver, depending on
    // who owns the schedule.

    [EnumDataType(typeof(GenderPolicy))]
    public GenderPolicy GenderPolicy { get; set; } = GenderPolicy.Any;

    /// <summary>
    /// Rider-owned schedules only: who else may be aboard. A rider's posting
    /// carries two conditions where a driver's trip carries one, so the
    /// generated row needs both stated here. Ignored on a driver's schedule.
    /// </summary>
    [EnumDataType(typeof(GenderPolicy))]
    public GenderPolicy CoRiderGenderPolicy { get; set; } = GenderPolicy.Any;

    [Range(RideAgeBounds.Min, RideAgeBounds.Max)]
    public int? MinAge { get; set; }

    [Range(RideAgeBounds.Min, RideAgeBounds.Max)]
    public int? MaxAge { get; set; }

    /// <summary>Generation is stopped; what already exists stands.</summary>
    public bool IsPaused { get; set; }
}

/// <summary>A schedule as its owner reads it back.</summary>
public class TripScheduleRow
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public ActiveRole OwnerRole { get; set; }

    public string OriginAddress { get; set; } = string.Empty;
    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestinationLat { get; set; }
    public double DestinationLng { get; set; }

    public Recurrence Recurrence { get; set; }
    public WeekDays DaysOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public TimeOnly TimeOfDay { get; set; }
    public string? TimeZoneId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int Seats { get; set; }

    public decimal? PricePerSeat { get; set; }
    public int? VehicleId { get; set; }
    public int MinSeatsToConfirm { get; set; }

    public GenderPolicy GenderPolicy { get; set; }
    public GenderPolicy CoRiderGenderPolicy { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    public bool IsPaused { get; set; }

    /// <summary>
    /// The last date generation has reached. Shown because a schedule that has
    /// stopped producing trips — paused, ended, or clashing every time — is
    /// otherwise invisible to its owner.
    /// </summary>
    public DateOnly? MaterialisedThrough { get; set; }

    /// <summary>
    /// The next few departures this schedule will produce, as instants.
    /// Computed, never stored: a schedule is a generator, and the only honest
    /// way to show what it means is to run it.
    /// </summary>
    public List<DateTime> NextDepartures { get; set; } = [];

    public TripScheduleRow() { }

    public TripScheduleRow(TripSchedule schedule, IEnumerable<DateTime>? nextDepartures = null)
    {
        if (schedule == null) return;

        Id = schedule.Id;
        OwnerId = schedule.OwnerId;
        OwnerRole = schedule.OwnerRole;
        OriginAddress = schedule.OriginAddress;
        OriginLat = schedule.Origin.Y;
        OriginLng = schedule.Origin.X;
        DestinationAddress = schedule.DestinationAddress;
        DestinationLat = schedule.Destination.Y;
        DestinationLng = schedule.Destination.X;
        Recurrence = schedule.Recurrence;
        DaysOfWeek = schedule.DaysOfWeek;
        DayOfMonth = schedule.DayOfMonth;
        TimeOfDay = schedule.TimeOfDay;
        TimeZoneId = schedule.TimeZoneId;
        StartDate = schedule.StartDate;
        EndDate = schedule.EndDate;
        Seats = schedule.Seats;
        PricePerSeat = schedule.PricePerSeat;
        VehicleId = schedule.VehicleId;
        MinSeatsToConfirm = schedule.MinSeatsToConfirm;
        GenderPolicy = schedule.GenderPolicy;
        CoRiderGenderPolicy = schedule.CoRiderGenderPolicy;
        MinAge = schedule.MinAge;
        MaxAge = schedule.MaxAge;
        IsPaused = schedule.IsPaused;
        MaterialisedThrough = schedule.MaterialisedThrough;
        NextDepartures = nextDepartures?.ToList() ?? [];
    }
}
