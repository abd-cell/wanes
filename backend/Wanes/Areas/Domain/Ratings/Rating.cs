using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Ratings;

/// <summary>A one-time, two-way rating unlocked after a booking completes.</summary>
public class Rating : BaseEntity
{
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public int FromUserId { get; set; }
    public User? FromUser { get; set; }

    public int ToUserId { get; set; }
    public User? ToUser { get; set; }

    public RatingDirection Direction { get; set; }
    public int Stars { get; set; }            // 1..5
    public string? Comment { get; set; }
}
