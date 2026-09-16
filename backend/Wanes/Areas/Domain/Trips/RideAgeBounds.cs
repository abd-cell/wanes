namespace Wanes.Areas.Domain.Trips;

/// <summary>
/// What an age condition may say — on a driver's trip, on a rider's request, or
/// on a recurring schedule of either kind.
///
/// Not a domain rule so much as a sanity bound: the field is a preference, not a
/// filter language, and "18 to 300" is a typo rather than an intention. It lives
/// in the domain rather than beside any one input because all four sides state
/// the same condition, and two of them stating it as two different ranges is how
/// a search stops agreeing with the booking it offers.
/// </summary>
public static class RideAgeBounds
{
    public const int Min = 16;
    public const int Max = 99;
}
