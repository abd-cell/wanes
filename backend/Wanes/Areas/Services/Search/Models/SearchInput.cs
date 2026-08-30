using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Search.Models;

public class SearchInput
{
    public GeoPoint Origin { get; set; } = new();
    public GeoPoint Destination { get; set; } = new();
    public DateTime When { get; set; } = DateTime.UtcNow;
    public int Seats { get; set; } = 1;
}
