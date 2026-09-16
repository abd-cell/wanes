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
    public const string TokenRefreshed = "auth.token_refreshed";
    public const string RefreshReuseDetected = "auth.refresh_reuse_detected";

    // profile
    public const string ProfileUpdate = "profile.update";
    public const string ProfilePreferences = "profile.preferences";
    public const string ProfileSwitchRole = "profile.switch_role";

    // saved places
    public const string PlaceAdd = "place.add";
    public const string PlaceDelete = "place.delete";

    // driver + vehicles
    public const string DriverApply = "driver.apply";
    public const string DriverDocumentUpload = "driver.document_upload";
    public const string DriverDocumentDelete = "driver.document_delete";
    public const string VehicleAdd = "vehicle.add";
    public const string VehicleUpdate = "vehicle.update";
    public const string VehicleDelete = "vehicle.delete";

    // trips
    public const string TripCreate = "trip.create";
    public const string TripUpdate = "trip.update";
    public const string TripCancel = "trip.cancel";
    /// <summary>The driver set off for the first pickup.</summary>
    public const string TripDepart = "trip.depart";

    public const string TripStart = "trip.start";
    public const string TripArrive = "trip.arrive";
    public const string TripComplete = "trip.complete";

    /// <summary>The seat threshold was met, or the driver chose to run without it.</summary>
    public const string TripConfirm = "trip.confirm";

    /// <summary>
    /// Called off for want of riders — by the driver at the decision prompt, or
    /// by the sweeper when the cutoff passed unanswered. The sweeper's rows
    /// carry no actor.
    /// </summary>
    public const string TripCancelLowSeats = "trip.cancel_low_seats";

    // search
    public const string SearchCarpool = "search.carpool";

    /// <summary>A driver searching for riders going their way.</summary>
    public const string SearchDemand = "search.demand";

    // bookings
    public const string BookingConfirm = "booking.confirm";
    public const string BookingCancel = "booking.cancel";

    // per-rider tracking, by the trip's driver
    public const string BookingArrive = "booking.arrive";
    public const string BookingPickUp = "booking.pickup";
    public const string BookingDropOff = "booking.dropoff";
    public const string BookingNoShow = "booking.no_show";

    // demand — ride requests (§7, §8)
    public const string RideRequestCreate = "request.create";
    public const string RideRequestJoin = "request.join";
    public const string RideRequestLeave = "request.leave";
    public const string RideRequestCancel = "request.cancel";

    /// <summary>
    /// Its departure came with nobody driving it. Logged by the sweeper, so it
    /// has no actor.
    /// </summary>
    public const string RideRequestExpire = "request.expire";

    /// <summary>A driver said they were willing to serve a request, at a price.</summary>
    public const string DriverInterest = "driver.interest";

    public const string DriverWithdraw = "driver.withdraw";

    /// <summary>
    /// One offer became *the* driver. Separate from the trip creation it causes,
    /// because a dispute about who should have got the ride is a question about
    /// this moment and not about the trip that came out of it.
    /// </summary>
    public const string DriverSelect = "driver.select";

    /// <summary>Demand became supply: the trip formed from a matched request.</summary>
    public const string TripForm = "trip.form";

    // schedules
    public const string ScheduleCreate = "schedule.create";
    public const string ScheduleUpdate = "schedule.update";
    public const string ScheduleDelete = "schedule.delete";

    /// <summary>
    /// The worker turned one occurrence of a schedule into a real row. Recorded
    /// against the schedule, with the generated trip in the payload: a series
    /// that quietly stops producing trips is otherwise invisible.
    /// </summary>
    public const string ScheduleMaterialise = "schedule.materialise";

    // notifications
    /// <summary>A user cleared one notification off their own inbox (soft delete).</summary>
    public const string NotificationDelete = "notification.delete";

    // ratings
    public const string RatingCreate = "rating.create";

    // complaints + suggestions
    public const string FeedbackSubmit = "feedback.submit";

    // admin
    public const string AdminDriverVerified = "admin.driver_verified";
    public const string AdminDriverRejected = "admin.driver_rejected";

    /// <summary>
    /// An admin opened a driver's identity document. Logged because reading
    /// someone's id is itself an act worth being able to account for later —
    /// the approval trail says who decided, this says who looked.
    /// </summary>
    public const string AdminDriverDocumentViewed = "admin.driver_document_viewed";
}
