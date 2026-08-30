using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>Admin view of a rider's saved place (list + detail share one shape).</summary>
public class SavedPlaceRow
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? OwnerName { get; set; }
    public SavedPlaceLabel Label { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public DateTime CreationDate { get; set; }

    public SavedPlaceRow() { }

    public SavedPlaceRow(SavedPlace e)
    {
        Id = e.Id;
        UserId = e.UserId;
        Label = e.Label;
        Name = e.Name;
        Address = e.Address;
        Lat = e.Location.Y;
        Lng = e.Location.X;
        CreationDate = e.CreationDate;
    }
}

/// <summary>Create/update payload for a saved place.</summary>
public class SavedPlaceInput
{
    public int UserId { get; set; }
    public SavedPlaceLabel Label { get; set; } = SavedPlaceLabel.Custom;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
}
