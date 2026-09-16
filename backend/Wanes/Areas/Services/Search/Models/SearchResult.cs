using Wanes.Areas.Services.Trips.Models;

using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Services.RideRequests.Models;

namespace Wanes.Areas.Services.Search.Models;

/// <summary>
/// Everything a rider's search can offer, in one response.
///
/// Not a mode. Search used to answer either "here are trips" or "we opened a
/// hail for you", and the second was a consolation prize dressed as a result.
/// Now every search returns the same four things in the same shape: trips going
/// the rider's way, trips passing it, postings they can join, and — always —
/// the earliest they could post one themselves.
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Bookable trips, best first, each labelled with the band it is in. One
    /// ordered list rather than a list per tier: the ranking already keeps the
    /// bands apart, and a client that wants sections can group on
    /// <see cref="SearchMatch.Tier"/> without the server deciding how many
    /// sections there are.
    /// </summary>
    public List<SearchMatch> Matches { get; set; } = [];

    /// <summary>
    /// **Tier 3** — open ride requests along the same route this rider could
    /// join instead of creating a near-identical one.
    ///
    /// A band of the same search, not a different screen. "Somebody already
    /// asked for this, join them" is a search result, and sending the rider
    /// elsewhere to find it is how a marketplace fragments into one request per
    /// rider.
    /// </summary>
    public List<RideRequestRow> Requests { get; set; } = [];

    /// <summary>
    /// The earliest departure this rider could post for, given the seats and
    /// distance they just searched.
    ///
    /// Sent on every search, not only an empty one. The client needs it while
    /// the rider is still choosing a time — being told "not before 08:40" as
    /// you pick is help; being refused after you tap Post is a rebuke.
    /// </summary>
    public DateTime EarliestDepartAt { get; set; }
}

/// <summary>One matched trip, and what matching it cost the rider.</summary>
public class SearchMatch
{
    public SearchTier Tier { get; set; }

    public TripOutput Trip { get; set; } = new();

    /// <summary>
    /// How far the rider walks at each end, in kilometres.
    ///
    /// For a corridor match these are measured to the points on the route the
    /// driver will actually pass, not to the driver's own endpoints — which is
    /// the whole difference between the two tiers, and the number the rider is
    /// really choosing on.
    /// </summary>
    public double PickupWalkKm { get; set; }

    public double DropoffWalkKm { get; set; }

    /// <summary>
    /// Where on the route the driver would pick this rider up and drop them off,
    /// on a corridor match. Null on a direct one, where the trip's own endpoints
    /// already say it.
    /// </summary>
    public double? PickupLat { get; set; }

    public double? PickupLng { get; set; }
    public double? DropoffLat { get; set; }
    public double? DropoffLng { get; set; }
}
