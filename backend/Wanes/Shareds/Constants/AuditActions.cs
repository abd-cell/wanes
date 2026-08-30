namespace Wanes.Shareds.Constants;

/// <summary>
/// Single source of truth for audit action names, so the audit vocabulary is
/// consistent and typo-proof across services (no scattered magic strings).
/// </summary>
public static class AuditActions
{
    // auth
    public const string OtpRequested = "auth.otp_requested";
    public const string Login = "auth.login";
    public const string Logout = "auth.logout";

    // profile
    public const string ProfileUpdate = "profile.update";
    public const string ProfilePreferences = "profile.preferences";
    public const string ProfileSwitchRole = "profile.switch_role";

    // saved places
    public const string PlaceAdd = "place.add";
    public const string PlaceDelete = "place.delete";

    // driver + vehicles
    public const string DriverApply = "driver.apply";
    public const string VehicleAdd = "vehicle.add";
    public const string VehicleUpdate = "vehicle.update";
    public const string VehicleDelete = "vehicle.delete";

    // trips
    public const string TripCreate = "trip.create";
    public const string TripUpdate = "trip.update";
    public const string TripCancel = "trip.cancel";
    public const string TripStart = "trip.start";
    public const string TripComplete = "trip.complete";

    // search
    public const string SearchCarpool = "search.carpool";
    public const string SearchHail = "search.hail";

    // bookings
    public const string BookingConfirm = "booking.confirm";
    public const string BookingCancel = "booking.cancel";

    // ride requests
    public const string RequestCancel = "request.cancel";
    public const string RequestAccept = "request.accept";

    // ratings
    public const string RatingCreate = "rating.create";

    // admin
    public const string AdminDriverVerified = "admin.driver_verified";
    public const string AdminDriverRejected = "admin.driver_rejected";
}
