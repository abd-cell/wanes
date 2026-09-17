namespace Wanes.Areas.Services.Bookings.Models;

public class CreateBookingInput
{
    public int TripId { get; set; }
    public int Seats { get; set; } = 1;

    /// <summary>The rider agreed the trip is shared. Recorded on the seat; older clients send nothing.</summary>
    public bool? AcceptSharedRide { get; set; }
}
