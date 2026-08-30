using NetTopologySuite.Geometries;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Domain.Users;

/// <summary>A rider's Home / Work / favourite place, to pre-fill search.</summary>
public class SavedPlace : BaseEntity
{
    public int UserId { get; set; }
    public User? User { get; set; }

    public SavedPlaceLabel Label { get; set; } = SavedPlaceLabel.Custom;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public Point Location { get; set; } = default!;   // SRID 4326
}
