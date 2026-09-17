using System.ComponentModel.DataAnnotations;
using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.Schedules;
using Wanes.Areas.Domain.Series;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Series.Models;

/// <summary>A driver offering to drive a rider's whole recurring request.</summary>
public class ProposeSeriesInput
{
    public int? VehicleId { get; set; }

    [Range(typeof(decimal), "0", "1000")]
    public decimal? PricePerSeat { get; set; }

    /// <summary>Seats on each day's trip; null offers the whole car.</summary>
    [Range(1, RiderTripRules.MaxSeats)]
    public int? SeatsOffered { get; set; }

    /// <summary>A subset of the schedule's days; None takes every day it runs.</summary>
    public WeekDays DaysOfWeek { get; set; } = WeekDays.None;

    /// <summary>The last day the driver commits to; null runs with the schedule.</summary>
    public DateOnly? Until { get; set; }

    [MaxLength(300)]
    public string? Message { get; set; }

    /// <summary>Required while <c>RequireSharedTermsAcceptance</c> is on, as for a one-day offer.</summary>
    public bool? AcceptSharedTrip { get; set; }
}

/// <summary>A rider booking every day of a driver's recurring trip.</summary>
public class JoinSeriesInput
{
    [Range(1, RiderTripRules.MaxSeats)]
    public int Seats { get; set; } = 1;

    public WeekDays DaysOfWeek { get; set; } = WeekDays.None;

    public DateOnly? Until { get; set; }

    public bool? AcceptSharedRide { get; set; }
}

public class EndSeriesInput
{
    /// <summary>
    /// False (the default) keeps the days inside the notice period and ends
    /// after them — free. True drops every upcoming day now.
    /// </summary>
    public bool Immediately { get; set; }

    [EnumDataType(typeof(CancelReason))]
    public CancelReason? Reason { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

/// <summary>A commitment as either side, or the admin console, reads it.</summary>
public class SeriesRow
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    public SeriesSide Side { get; set; }
    public SeriesStatus Status { get; set; }

    public int DriverId { get; set; }
    public string? DriverName { get; set; }
    public double DriverRating { get; set; }
    public int DriverTrips { get; set; }
    public double? DriverCompletionRate { get; set; }

    public int RiderId { get; set; }
    public string? RiderName { get; set; }

    public string? VehicleLabel { get; set; }
    public string? VehicleColor { get; set; }
    public int? VehicleSeats { get; set; }

    public decimal? PricePerSeat { get; set; }
    public int? Seats { get; set; }
    public WeekDays DaysOfWeek { get; set; }
    public DateOnly? Until { get; set; }
    public string? Message { get; set; }

    public DateTime? DecideAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? EndRequestedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public CancelReason? EndReason { get; set; }

    // The schedule it rides on.
    public string OriginAddress { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public Recurrence Recurrence { get; set; }
    public WeekDays ScheduleDaysOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public TimeOnly TimeOfDay { get; set; }
    public string? TimeZoneId { get; set; }
    public DateOnly? ScheduleEndDate { get; set; }
    public bool SchedulePaused { get; set; }

    /// <summary>Upcoming days already written under this commitment — trips (driver) or seats (rider).</summary>
    public int UpcomingCount { get; set; }
    public DateTime? NextDeparture { get; set; }

    /// <summary>The caller made this commitment (as opposed to owning the schedule it is on).</summary>
    public bool IsMine { get; set; }

    public DateTime CreatedAt { get; set; }

    public SeriesRow() { }

    public SeriesRow(SeriesCommitment c, TripSchedule? schedule, int callerId)
    {
        Id = c.Id;
        ScheduleId = c.ScheduleId;
        Side = c.Side;
        Status = c.Status;
        DriverId = c.DriverId;
        if (c.Driver != null)
        {
            DriverName = c.Driver.DisplayName ?? c.Driver.FirstName;
            DriverRating = c.Driver.RatingAvg;
            DriverTrips = c.Driver.TripsAsDriver;
            DriverCompletionRate = ReliabilityRules.CompletionRate(c.Driver.TripsAsDriver, c.Driver.DriverCancellations);
        }
        RiderId = c.RiderId;
        RiderName = c.Rider == null ? null : c.Rider.DisplayName ?? c.Rider.FirstName;
        if (c.Vehicle != null)
        {
            VehicleLabel = $"{c.Vehicle.Make} {c.Vehicle.Model}".Trim();
            VehicleColor = c.Vehicle.Color;
            VehicleSeats = c.Vehicle.SeatCapacity;
        }
        PricePerSeat = c.PricePerSeat;
        Seats = c.Seats;
        DaysOfWeek = c.DaysOfWeek;
        Until = c.Until;
        Message = c.Message;
        DecideAt = c.DecideAt;
        AcceptedAt = c.AcceptedAt;
        EndRequestedAt = c.EndRequestedAt;
        EndedAt = c.EndedAt;
        EndReason = c.EndReason;
        IsMine = c.CommitterId == callerId;
        CreatedAt = c.CreationDate;

        if (schedule != null)
        {
            OriginAddress = schedule.OriginAddress;
            DestinationAddress = schedule.DestinationAddress;
            Recurrence = schedule.Recurrence;
            ScheduleDaysOfWeek = schedule.DaysOfWeek;
            DayOfMonth = schedule.DayOfMonth;
            TimeOfDay = schedule.TimeOfDay;
            TimeZoneId = schedule.TimeZoneId;
            ScheduleEndDate = schedule.EndDate;
            SchedulePaused = schedule.IsPaused;
        }
    }
}

/// <summary>What one day of a series came to.</summary>
public class SeriesDayResult
{
    public DateOnly Date { get; set; }
    public DateTime DepartAt { get; set; }
    public int? TripId { get; set; }
    public int? BookingId { get; set; }

    /// <summary>Why the day could not be taken or booked — busy, full, not eligible.</summary>
    public ErrorCodeName? Refusal { get; set; }
}

/// <summary>An error code by value and name, so a client can word it without a lookup table.</summary>
public record ErrorCodeName(int Code, string Name);

public class SeriesResult
{
    public SeriesRow Series { get; set; } = new();
    public List<SeriesDayResult> Days { get; set; } = [];
}

/// <summary>What ending a series would do, both ways — read before the button.</summary>
public class SeriesEndPreview
{
    public int NoticeDays { get; set; }

    /// <summary>The last day that still runs when ending with notice.</summary>
    public DateOnly NoticeEnd { get; set; }

    /// <summary>Upcoming days kept by ending with notice.</summary>
    public int DaysKept { get; set; }

    /// <summary>Upcoming days dropped by ending with notice — free.</summary>
    public int DaysDroppedWithNotice { get; set; }

    /// <summary>Upcoming days dropped by ending now.</summary>
    public int DaysDroppedNow { get; set; }

    /// <summary>What ending now costs the caller: points (driver) or late cancellations (rider).</summary>
    public int PointsNow { get; set; }
    public int LateCancelsNow { get; set; }

    public int PointsAfter { get; set; }
    public int SuspendPoints { get; set; }
    public bool WouldSuspend { get; set; }

    /// <summary>The caller ends without any cost either way (the rider releasing their driver, an admin).</summary>
    public bool Free { get; set; }
}

/// <summary>
/// The recurrence behind a card — what the marketplace, a trip or a seat
/// shows as "Repeats Sun–Thu · until 31 Dec", and whether the caller can
/// commit to the whole of it.
/// </summary>
public class SeriesInfo
{
    public int ScheduleId { get; set; }
    public ActiveRole OwnerRole { get; set; }
    public Recurrence Recurrence { get; set; }
    public WeekDays DaysOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public TimeOnly TimeOfDay { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsPaused { get; set; }

    /// <summary>Generated days still ahead — open requests, or bookable trips.</summary>
    public int UpcomingDays { get; set; }

    /// <summary>The series already has a driver (rider's schedule).</summary>
    public bool HasDriver { get; set; }
    public string? DriverName { get; set; }

    /// <summary>Drivers who offered for the whole series and are waiting.</summary>
    public int ProposalCount { get; set; }

    /// <summary>The caller's own live commitment or proposal on it.</summary>
    public int? MySeriesId { get; set; }
    public SeriesStatus? MySeriesStatus { get; set; }

    /// <summary>The commitment the row itself was made under, when there is one.</summary>
    public int? CommitmentId { get; set; }
}
