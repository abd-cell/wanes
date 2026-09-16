using Wanes.Areas.Domain.Users;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Domain.Trips;

/// <summary>
/// Whether these two people may share a car, given what each of them asked for.
///
/// Conditions run both ways: a driver says who may take their seats, and a rider
/// says who they will ride with and who may drive them. Both directions are
/// checked here, in one place, for the reason
/// <see cref="Wanes.Shareds.Constants.MatchRules"/> exists at all — the same
/// question is asked by search (to filter), by booking (to refuse), by a claim
/// (to convert holds into bookings) and by the schedule materialiser, and copies
/// of it will drift. The drift shows up as a rider who can see a trip they
/// cannot book, or a driver holding a card they cannot answer.
///
/// Two rules underpin the whole thing:
///
/// <list type="bullet">
/// <item><b>A stated requirement needs real data.</b> A female-only trip refuses
/// a rider who has not said, rather than guessing. The app's answer is to ask
/// for the field at that moment — not to hide the trip forever.</item>
/// <item><b>Nobody is judged on a birthday they have not had.</b> Age is taken at
/// the trip's departure, so a rider who qualifies when they book still qualifies
/// when they travel.</item>
/// </list>
/// </summary>
public static class RiderEligibilityRules
{
    /// <summary>
    /// The policy that admits exactly this gender, or <c>null</c> for somebody
    /// who has not said — they are admitted by <see cref="GenderPolicy.Any"/>
    /// and by nothing else.
    ///
    /// Queries use this rather than comparing enums across types: the numeric
    /// values happen to line up today, and a filter that quietly relies on that
    /// would break the moment either enum grows a value.
    /// </summary>
    public static GenderPolicy? PolicyFor(Gender gender) => gender switch
    {
        Gender.Male => GenderPolicy.MaleOnly,
        Gender.Female => GenderPolicy.FemaleOnly,
        _ => null,
    };

    /// <summary>Whether <paramref name="policy"/> admits <paramref name="gender"/>.</summary>
    public static bool Admits(GenderPolicy policy, Gender gender) =>
        policy == GenderPolicy.Any || PolicyFor(gender) == policy;

    /// <summary>
    /// Whole years old at <paramref name="at"/>, or <c>null</c> for a profile
    /// with no date of birth.
    /// </summary>
    public static int? AgeAt(DateTime? dateOfBirth, DateTime at)
    {
        if (dateOfBirth == null) return null;
        var dob = dateOfBirth.Value.Date;
        var age = at.Year - dob.Year;
        if (at.Date < dob.AddYears(age)) age--;
        return age < 0 ? 0 : age;
    }

    /// <summary>
    /// Whether an age bound is satisfied. A bound with no age to check against
    /// fails: the condition asked a question the profile cannot answer.
    /// </summary>
    public static bool WithinAge(int? age, int? minAge, int? maxAge)
    {
        if (minAge == null && maxAge == null) return true;
        if (age == null) return false;
        return (minAge == null || age >= minAge) && (maxAge == null || age <= maxAge);
    }

    /// <summary>
    /// The driver's conditions, checked against the rider taking a seat.
    /// <c>null</c> means "may ride"; otherwise the refusal to return verbatim.
    ///
    /// <see cref="ErrorCode.RiderProfileIncomplete"/> and
    /// <see cref="ErrorCode.RiderNotEligible"/> are deliberately different
    /// answers: one is fixable by the rider in thirty seconds, the other is not
    /// fixable at all, and a client that cannot tell them apart either nags
    /// people about their profile for no reason or leaves a fixable refusal
    /// looking final.
    /// </summary>
    public static ErrorCode? CheckRider(User rider, RideConditions conditions, DateTime departAt)
    {
        if (conditions.GenderPolicy != GenderPolicy.Any && rider.Gender == Gender.Unspecified)
            return ErrorCode.RiderProfileIncomplete;
        if (!Admits(conditions.GenderPolicy, rider.Gender))
            return ErrorCode.RiderNotEligible;

        var hasAgeBound = conditions.MinAge != null || conditions.MaxAge != null;
        if (hasAgeBound && rider.DateOfBirth == null)
            return ErrorCode.RiderProfileIncomplete;
        if (!WithinAge(AgeAt(rider.DateOfBirth, departAt), conditions.MinAge, conditions.MaxAge))
            return ErrorCode.RiderNotEligible;

        return null;
    }

    /// <summary>
    /// The rider's condition on who drives them. <c>null</c> means "may drive".
    ///
    /// Only gender: a rider choosing their driver is choosing who they get in a
    /// car with, which is the whole point of the field. An age bound on the
    /// driver was left out on purpose — it reads as a proxy for experience,
    /// which the rating already answers better.
    /// </summary>
    public static ErrorCode? CheckDriver(User driver, GenderPolicy policy)
    {
        if (policy == GenderPolicy.Any) return null;
        if (driver.Gender == Gender.Unspecified) return ErrorCode.DriverNotEligible;
        return Admits(policy, driver.Gender) ? null : ErrorCode.DriverNotEligible;
    }

    /// <summary>
    /// Whether a rider is admitted by <paramref name="conditions"/> — the
    /// boolean form, for callers filtering a list rather than refusing a call.
    /// </summary>
    public static bool CanRide(User rider, RideConditions conditions, DateTime departAt) =>
        CheckRider(rider, conditions, departAt) == null;
}

/// <summary>
/// The conditions one side puts on the other, as a value.
///
/// A record rather than three loose parameters: a trip carries a set of these,
/// so does a rider-posted trip, so does a schedule, and every check takes the
/// whole set. Passing them separately is how a caller ends up checking the
/// gender and forgetting the age.
/// </summary>
public readonly record struct RideConditions(GenderPolicy GenderPolicy, int? MinAge, int? MaxAge)
{
    /// <summary>No conditions at all — everybody is admitted.</summary>
    public static readonly RideConditions Open = new(GenderPolicy.Any, null, null);

    public bool IsOpen => GenderPolicy == GenderPolicy.Any && MinAge == null && MaxAge == null;

    /// <summary>
    /// The stricter of two condition sets: the gender policy that admits fewer
    /// people, and the narrower age band.
    ///
    /// This is what makes a pool's conditions *intersect* as riders join it
    /// rather than the last joiner's winning. Two opposed gender policies have
    /// no intersection at all, which is why this is only ever reached after the
    /// join has been checked against everybody already holding a seat.
    /// </summary>
    public RideConditions Tighten(RideConditions other) => new(
        GenderPolicy == GenderPolicy.Any ? other.GenderPolicy : GenderPolicy,
        Max(MinAge, other.MinAge),
        Min(MaxAge, other.MaxAge));

    private static int? Max(int? a, int? b) => a == null ? b : b == null ? a : Math.Max(a.Value, b.Value);

    private static int? Min(int? a, int? b) => a == null ? b : b == null ? a : Math.Min(a.Value, b.Value);
}
