using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Services.RideRequests.Models;

namespace Wanes.Areas.Services.Search.Models;

/// <summary>
/// A driver searching for riders — the mirror of <see cref="SearchInput"/>.
///
/// The rider states where they want to go and finds trips going that way; the
/// driver states where they are *going to drive* and finds people who want that
/// journey. Same two points, same question about a route, opposite side of the
/// car — which is why this reuses the corridor maths rather than inventing a
/// second notion of "on the way".
///
/// The driver's board (<c>GET ride-requests/nearby</c>) is a different thing and
/// stays: it answers "who needs a lift near me, now", off their live position and
/// with no route at all. This answers "I am driving Amman → Irbid at six, who is
/// going my way".
/// </summary>
public class DemandSearchInput
{
    /// <summary>Where the driver is setting off from.</summary>
    public GeoPoint Origin { get; set; } = new();

    /// <summary>Where they are going.</summary>
    public GeoPoint Destination { get; set; } = new();

    /// <summary>When they intend to leave.</summary>
    public DateTime When { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Seats they can offer. A request wanting more than this is not a match: one
    /// driver in one car has to be able to take the whole pool, and offering
    /// them a group of four when they have two seats is a refusal at the tap.
    ///
    /// Defaults to 0, which means "read it off my car" — the usual case, and one
    /// less thing for the driver to type.
    /// </summary>
    public int Seats { get; set; }

    /// <summary>
    /// Narrow match (5 km around each end) vs. wide (50 km). Narrow by default,
    /// as on the rider's side.
    ///
    /// Wide was tempting — a driver drives to the pickup where a rider walks to
    /// it — but it collapses the two bands into one: at 50 km on a 67 km leg,
    /// every pool in the region has both ends "near" the driver's, so everything
    /// is a direct match and "on the way" stops meaning anything. Narrow keeps
    /// the distinction the driver is actually choosing on, and widening is one
    /// tap away.
    /// </summary>
    public bool Nearby { get; set; } = true;

    /// <summary>Which order the matches come back in.</summary>
    public SearchSort SortBy { get; set; } = SearchSort.Best;

    /// <summary>
    /// Only show requests wanting at least this many seats. A driver filling a
    /// seven-seater may not want to divert for a single rider; one wanting
    /// company on a long leg may not care.
    /// </summary>
    public int MinSeats { get; set; }

    /// <summary>
    /// Who the driver will carry, for this search — their condition on the
    /// riders, stated per journey exactly as the rider's condition on their
    /// driver is (<see cref="SearchInput.DriverGenderPolicy"/>).
    ///
    /// A filter, not a rule: it shapes this list and nothing downstream
    /// re-checks it. The conditions that outlive a search are the ones written
    /// onto a row, and taking a trip still answers to those.
    /// </summary>
    public GenderPolicy RiderGenderPolicy { get; set; } = GenderPolicy.Any;
}

/// <summary>Everything a driver's search can offer, in one response.</summary>
public class DemandSearchResult
{
    /// <summary>
    /// Requests wanting this journey, best first, each labelled with the band it
    /// is in. One ordered list rather than a list per tier, for the same reason
    /// <see cref="SearchResult.Matches"/> is one list.
    /// </summary>
    public List<DemandMatch> Matches { get; set; } = [];

    /// <summary>
    /// Seats the driver would have left if they took every direct match. Shown
    /// so a driver scanning the list knows when they have filled the car.
    /// </summary>
    public int SeatsOffered { get; set; }
}

/// <summary>One ride request, and what serving it would cost the driver.</summary>
public class DemandMatch
{
    public SearchTier Tier { get; set; }

    public RideRequestRow Request { get; set; } = new();

    /// <summary>
    /// How far off the driver's own route each end sits, in kilometres — the
    /// diversion they are being asked for.
    ///
    /// On a direct match these are distances to the driver's own endpoints; on a
    /// corridor match they are measured to the points on the driver's route they
    /// will actually pass, which is the whole difference between the two bands.
    /// </summary>
    public double PickupDetourKm { get; set; }

    public double DropoffDetourKm { get; set; }

    /// <summary>
    /// Both ends together — what the driver is really choosing on, and what the
    /// ranking sorts by inside a band.
    /// </summary>
    public double TotalDetourKm { get; set; }

    /// <summary>
    /// How far the request's wanted departure is from the driver's, in minutes.
    /// Signed: negative means the riders want to leave earlier than the driver
    /// said.
    /// </summary>
    public int MinutesFromWhen { get; set; }
}
