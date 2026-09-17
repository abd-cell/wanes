using Wanes.Areas.Services.RideRequests.Models;

namespace Wanes.Tests.TestDoubles;

/// <summary>Driver offers as the app sends them: always agreeing the trip is shared.</summary>
public static class Offer
{
    public static ExpressInterestInput Shared(decimal? pricePerSeat = null) =>
        new() { AcceptSharedTrip = true, PricePerSeat = pricePerSeat };
}
