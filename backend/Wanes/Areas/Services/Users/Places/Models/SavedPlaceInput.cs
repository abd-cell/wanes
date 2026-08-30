using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Users.Places.Models;

public class SavedPlaceInput
{
    public SavedPlaceLabel Label { get; set; } = SavedPlaceLabel.Custom;
    public string Name { get; set; } = string.Empty;
    public GeoPoint Place { get; set; } = new();
}
