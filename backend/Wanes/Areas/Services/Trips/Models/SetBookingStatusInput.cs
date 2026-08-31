using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Trips.Models;

/// <summary>The seat's new state, as the driver reports it.</summary>
public class SetBookingStatusInput
{
    public BookingStatus Status { get; set; }
}
