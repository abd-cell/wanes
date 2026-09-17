using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Trips.Models;

/// <summary>The seat's new state, as the driver reports it.</summary>
public class SetBookingStatusInput
{
    public BookingStatus Status { get; set; }

    /// <summary>The four digits the rider read out — required to mark them aboard.</summary>
    [System.ComponentModel.DataAnnotations.StringLength(8)]
    public string? BoardingCode { get; set; }
}
