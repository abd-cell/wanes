namespace Wanes.Areas.Domain.Trips;

/// <summary>
/// The per-seat figure a trip is listed at when nobody quoted one.
///
/// A driver who posts a trip sets their own price, and that value is used
/// verbatim — this is not consulted. It exists for the other way a trip comes
/// into being: a driver accepting a hail never quoted a rate, they only said
/// yes. Leaving those trips unpriced made them the one kind of trip in search
/// with a blank where every other row shows a number, so the platform derives
/// one from the distance instead.
///
/// The rates are <c>AppConfiguration.FareBaseAmount</c> and
/// <c>FarePerKm</c> — an admin decision, like the currency they are quoted in,
/// because what a ride is worth is a market question and not a constant. The
/// values here are only what a fresh install starts with, and what a client
/// falls back to before it has read the configuration.
/// </summary>
public static class FareRules
{
    /// <summary>Flag-fall: what the ride costs before it has covered any ground.</summary>
    public const decimal DefaultBaseAmount = 2.50m;

    /// <summary>Rate per kilometre on top of the flag-fall.</summary>
    public const decimal DefaultPerKm = 1.20m;

    /// <summary>
    /// Both rates are floored at zero — a negative rate would price a long ride
    /// below a short one — and capped well above any sane fare so a slipped
    /// decimal in the CMS cannot list a trip at a fortune.
    /// </summary>
    public const decimal MinRate = 0m;

    public const decimal MaxRate = 1000m;

    /// <summary>
    /// What one seat on a <paramref name="km"/>-long ride is listed at.
    ///
    /// Per *seat*, not per trip: a rider taking two seats pays this twice, and
    /// the multiplication belongs to whoever is showing a total. Rounded to two
    /// places because that is what the column stores
    /// (<c>Trip.PricePerSeat</c> is <c>decimal(10,2)</c>) — deriving more
    /// precision than can be saved would mean the figure quoted and the figure
    /// stored disagreed.
    /// </summary>
    public static decimal PerSeat(double km, decimal baseAmount, decimal perKm)
    {
        var distance = double.IsFinite(km) && km > 0 ? km : 0;
        var fare = baseAmount + perKm * (decimal)distance;
        return Math.Round(fare < 0 ? 0 : fare, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>An admin-set rate, clamped to something a trip can sanely list at.</summary>
    public static decimal RateFor(decimal rate) => Math.Clamp(rate, MinRate, MaxRate);

    /// <summary>
    /// A price a driver typed, made storable: clamped to the same bounds as the
    /// rates and rounded to the two places the column holds.
    ///
    /// Zero is kept rather than treated as "unset" — a driver charging nothing
    /// is a favour, not a missing field, and the two have to stay tellable apart.
    /// </summary>
    public static decimal PriceFor(decimal price) =>
        Math.Round(Math.Clamp(price, MinRate, MaxRate), 2, MidpointRounding.AwayFromZero);
}
