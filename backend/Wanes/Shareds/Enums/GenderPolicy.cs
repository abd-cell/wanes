namespace Wanes.Shareds.Enums;

/// <summary>
/// Who a ride is open to, as a condition one side sets on the other.
///
/// The same three values express both directions — a driver's condition on
/// their riders and a rider's condition on their driver and co-riders — so
/// there is one vocabulary to read, one to translate, and one to check
/// (<c>Areas/Domain/Trips/RiderEligibilityRules</c>).
///
/// Distinct from <see cref="Gender"/>, which is what a person *is*:
/// <see cref="Any"/> has no counterpart there, and <see cref="Gender.Unspecified"/>
/// has none here — a policy of "unspecified" would be unanswerable, while a
/// person who has not said is simply refused a restricted ride.
/// </summary>
public enum GenderPolicy
{
    Any = 0,
    MaleOnly = 1,
    FemaleOnly = 2,
}
