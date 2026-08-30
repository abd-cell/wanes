using Wanes.Areas.Domain.Ratings;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>Admin view of a rating (list + detail share one shape).</summary>
public class RatingRow
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string? BookingSummary { get; set; }
    public int FromUserId { get; set; }
    public string? FromName { get; set; }
    public int ToUserId { get; set; }
    public string? ToName { get; set; }
    public RatingDirection Direction { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public DateTime CreationDate { get; set; }

    public RatingRow() { }

    public RatingRow(Rating e)
    {
        Id = e.Id;
        BookingId = e.BookingId;
        FromUserId = e.FromUserId;
        ToUserId = e.ToUserId;
        Direction = e.Direction;
        Stars = e.Stars;
        Comment = e.Comment;
        CreationDate = e.CreationDate;
    }
}

/// <summary>Create/update payload for a rating.</summary>
public class RatingInput
{
    public int BookingId { get; set; }
    public int FromUserId { get; set; }
    public int ToUserId { get; set; }
    public RatingDirection Direction { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
}
