using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Search.Models;

public class SearchInput
{
    public GeoPoint Origin { get; set; } = new();
    public GeoPoint Destination { get; set; } = new();
    public DateTime When { get; set; } = DateTime.UtcNow;
    public int Seats { get; set; } = 1;

    /// <summary>
    /// Narrow match (5 km around each end) vs. wide (50 km). Riders default to
    /// narrow — a wide match usually means a long walk at one end of the trip.
    /// </summary>
    public bool Nearby { get; set; } = true;

    /// <summary>
    /// Which order the matches come back in. Defaults to the combined-proximity
    /// ranking; the app sends back whatever the rider last chose.
    /// </summary>
    public SearchSort SortBy { get; set; } = SearchSort.Best;

    /// <summary>
    /// Who the rider will get in a car with, for this search.
    ///
    /// It used to be an account setting, edited on a profile screen and applied
    /// to every search forever. That is the wrong shape for it: who you are
    /// willing to travel with is a decision about *this* journey — the airport
    /// run at dawn and the commute home are not the same question — and a
    /// setting buried two screens away is one nobody remembers is on.
    ///
    /// Filters both bands of trip and the postings offered to join. Nothing
    /// downstream re-checks it: the rider is choosing a named driver off a list
    /// they just filtered, so there is no stale screen for it to protect
    /// against. The conditions that outlive the search are the ones written
    /// onto a row — the driver's on their trip, the riders' on their posting —
    /// and those are still enforced at the tap.
    /// </summary>
    public GenderPolicy DriverGenderPolicy { get; set; } = GenderPolicy.Any;
}
