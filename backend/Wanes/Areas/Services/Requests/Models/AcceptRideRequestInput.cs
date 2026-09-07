using System.ComponentModel.DataAnnotations;
using Wanes.Areas.Domain.Trips;

namespace Wanes.Areas.Services.Requests.Models;

/// <summary>
/// What a driver says when they take a hail. Only the price — everything else
/// about the trip comes from the request and the driver's vehicle.
/// </summary>
public class AcceptRideRequestInput
{
    /// <summary>
    /// What the driver is charging per seat.
    ///
    /// Nullable, and not because it is optional to the driver: the app requires
    /// it and pre-fills the distance estimate so there is always a figure to
    /// confirm. It is nullable so the server still has an answer when the field
    /// does not arrive — an older build, or a retried call — and falls back to
    /// deriving it (<see cref="FareRules"/>) rather than creating the one kind
    /// of trip that lists with a blank price.
    ///
    /// Bounded the same way the admin rates are: zero is a real choice (a
    /// favour, a colleague), negative is not, and the ceiling stops a slipped
    /// decimal listing a seat at a fortune.
    /// </summary>
    [Range(typeof(decimal), "0", "1000")]
    public decimal? PricePerSeat { get; set; }
}
