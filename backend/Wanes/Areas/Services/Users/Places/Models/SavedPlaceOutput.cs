using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;

namespace Wanes.Areas.Services.Users.Places.Models;

public class SavedPlaceOutput
{
    public int Id { get; set; }
    public SavedPlaceLabel Label { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }

    public SavedPlaceOutput() { }

    public SavedPlaceOutput(SavedPlace place)
    {
        if (place == null) return;

        Id = place.Id;
        Label = place.Label;
        Name = place.Name;
        Address = place.Address;
        Lat = place.Location.Y;
        Lng = place.Location.X;
    }
}
