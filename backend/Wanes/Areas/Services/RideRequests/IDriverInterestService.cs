using Wanes.Areas.Domain.Series;
using Wanes.Areas.Services.Marketplace.Models;
using Wanes.Areas.Services.RideRequests.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.RideRequests;

/// <summary>
/// The supply side of demand: a driver offers to serve a request, one offer is
/// selected, and the selected one becomes a trip.
///
/// Three steps rather than one because that is what makes "first click wins" a
/// setting instead of the architecture (<c>DriverSelectionRules</c>). With the
/// selection window at zero — the shipped value — a driver taps once and gets a
/// trip, exactly as the old claim behaved; every step below still happens, in
/// the same transaction.
/// </summary>
[TransientInjectable]
public interface IDriverInterestService
{
    /// <summary>
    /// Records that this driver is willing to serve the request, at their price
    /// and in their car — and, when the marketplace selects immediately, decides
    /// it on the spot.
    ///
    /// Idempotent for the same driver: a double tap, or a retried request whose
    /// response was never seen, is answered from the offer they already have,
    /// or from the trip it already became.
    /// </summary>
    Task<BaseResponse<RideRequestRow>> ExpressInterest(int requestId, ExpressInterestInput? input = null);

    /// <summary>
    /// Takes an offer back, while the request is still open and nobody has been
    /// selected. Refused afterwards — by then it is a trip with passengers on it,
    /// and the way out of that is cancelling the trip.
    /// </summary>
    Task<BaseResponse> WithdrawInterest(int requestId);

    /// <summary>
    /// Decides every request whose selection window has closed, and forms the
    /// trip. Returns how many were matched.
    ///
    /// Does nothing at all while the window is zero: selection happened inside
    /// <see cref="ExpressInterest"/>, and the sweep exists for the configuration
    /// where offers accumulate.
    /// </summary>
    Task<int> SelectDue();

    /// <summary>The live offers on a request, for its riders to compare. Participants only.</summary>
    Task<BaseResponse<List<OfferRow>>> GetOffers(int requestId);

    /// <summary>
    /// A rider picking one of the collected offers. Forms the trip with that
    /// driver at once, if they are still free to take it.
    /// </summary>
    Task<BaseResponse<RideRequestRow>> ChooseOffer(int requestId, int interestId);

    /// <summary>
    /// One day of a series the driver committed to: forms the trip with that
    /// driver outright, answering any one-day offers. No caller identity — the
    /// commitment is the authority, and the materialiser has nobody signed in.
    /// </summary>
    Task<SeriesDayOutcome> FormForSeries(int requestId, SeriesCommitment commitment);
}

/// <summary>What happened to one day of a series: the trip it became, or why not.</summary>
public record SeriesDayOutcome(int? TripId, ErrorCode? Refusal);
