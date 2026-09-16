using Wanes.Areas.Domain.Schedules;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>
/// Admin view of a recurring posting.
///
/// On the console because a schedule is the only row in the system that
/// explains a *pattern*: a rider with fourteen near-identical postings, or a
/// driver whose Tuesday trip keeps failing to appear, is a support question
/// nobody can answer from the generated rows alone.
/// </summary>
public class ScheduleRow
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public ActiveRole OwnerRole { get; set; }

    public string OriginAddress { get; set; } = string.Empty;
    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestLat { get; set; }
    public double DestLng { get; set; }

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
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    public bool IsPaused { get; set; }
    public DateOnly? MaterialisedThrough { get; set; }
    public DateTime CreationDate { get; set; }

    public ScheduleRow() { }

    public ScheduleRow(TripSchedule schedule)
    {
        Id = schedule.Id;
        OwnerId = schedule.OwnerId;
        OwnerRole = schedule.OwnerRole;
        OriginAddress = schedule.OriginAddress;
        OriginLat = schedule.Origin.Y;
        OriginLng = schedule.Origin.X;
        DestinationAddress = schedule.DestinationAddress;
        DestLat = schedule.Destination.Y;
        DestLng = schedule.Destination.X;
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
        MinAge = schedule.MinAge;
        MaxAge = schedule.MaxAge;
        IsPaused = schedule.IsPaused;
        MaterialisedThrough = schedule.MaterialisedThrough;
        CreationDate = schedule.CreationDate;
    }
}

/// <summary>
/// What an admin may change on a schedule: whether it runs, and when.
///
/// Deliberately narrow. The console does not write somebody's commute for them
/// — there is no create — and it does not touch the route or the conditions,
/// which are the owner's own statements about who they will travel with. What a
/// support desk actually needs is to stop a runaway series and to correct a time.
/// </summary>
public class ScheduleInput
{
    public bool IsPaused { get; set; }

    public TimeOnly? TimeOfDay { get; set; }

    public DateOnly? EndDate { get; set; }
}
