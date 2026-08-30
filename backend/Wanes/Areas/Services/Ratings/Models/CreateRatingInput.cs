namespace Wanes.Areas.Services.Ratings.Models;

public class CreateRatingInput
{
    public int BookingId { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
}
