namespace Wanes.Shareds.Models;

/// <summary>
/// Typed error codes. Callers switch on this instead of parsing messages.
/// Localized messages resolve from Resources/General/General.{en,ar}.json by name.
/// </summary>
public enum ErrorCode
{
    Success = 0,
    None = 0,

    // Generic
    UnknownError = 1,
    ValidationError = 2,
    NotFound = 3,
    Unauthorized = 4,
    Forbidden = 5,
    Conflict = 6,

    /// <summary>The upload is bigger than the endpoint will take.</summary>
    FileTooLarge = 7,

    /// <summary>The upload is not a file type the endpoint accepts, or its bytes contradict its declared type.</summary>
    UnsupportedFileType = 8,

    // Auth / accounts
    InvalidCredentials = 100,
    PhoneNotVerified = 101,
    InvalidOtp = 102,
    OtpExpired = 103,
    PhoneAlreadyRegistered = 104,
    SessionExpired = 105,
    AccountDisabled = 106,

    // Driver / vehicle
    DriverNotVerified = 200,
    VehicleNotFound = 202,

    /// <summary>The driver is out on a trip, so cannot take on another ride.</summary>
    DriverOnActiveTrip = 203,

    /// <summary>The driver already has a trip departing at about the same time.</summary>
    DriverTripTimeConflict = 204,

    /// <summary>The application is missing one of the documents a reviewer needs to decide.</summary>
    DriverDocumentsIncomplete = 205,

    /// <summary>The document is not this driver's, or no longer exists.</summary>
    DriverDocumentNotFound = 206,

    // Trips
    TripNotFound = 300,
    TripNotBookable = 301,
    DepartureMustBeFuture = 302,
    SeatsExceedCapacity = 303,
    OriginEqualsDestination = 304,
    GeocodingFailed = 305,
    TripNotEditable = 306,

    /// <summary>
    /// The trip is not waiting on a seat threshold, so there is nothing for the
    /// driver's run-or-cancel decision to answer.
    /// </summary>
    TripNotConfirmable = 307,

    /// <summary>More seats must confirm the trip than the trip offers.</summary>
    MinSeatsExceedTotal = 308,

    // Bookings
    BookingNotFound = 400,
    NoSeatsLeft = 401,
    CannotBookOwnTrip = 402,
    AlreadyBooked = 403,

    /// <summary>The driver asked for a seat move its current status does not allow.</summary>
    BookingStatusNotAllowed = 404,

    /// <summary>
    /// The trip's conditions ask for a profile detail this rider has not given
    /// (gender, date of birth). Refused rather than assumed.
    /// </summary>
    RiderProfileIncomplete = 406,

    /// <summary>The rider does not meet the driver's conditions for this trip.</summary>
    RiderNotEligible = 407,

    /// <summary>The driver does not meet the rider's conditions for this ride.</summary>
    DriverNotEligible = 408,

    /// <summary>
    /// The rider already holds a seat leaving at about this time. One rider
    /// rides in one car — see
    /// <c>Areas/Domain/Bookings/RiderAvailabilityRules</c>.
    /// </summary>
    RiderSeatTimeConflict = 409,

    /// <summary>
    /// The rider is on a ride right now, so no departure is open to them until
    /// it ends. Distinct from <see cref="RiderSeatTimeConflict"/> because it is
    /// a different sentence: not "not then" but "not yet".
    /// </summary>
    RiderOnActiveTrip = 410,

    // Demand — ride requests (§7). The numbers are unchanged from when these
    // were "rider-posted trips": the meanings are identical and every client
    // maps by number, so renaming the members costs nothing and renumbering
    // them would silently re-label every mapped string.
    RideRequestNotFound = 500,
    RideRequestNotOpen = 501,

    /// <summary>
    /// The departure is sooner than a driver could gather this many riders and
    /// run the leg — see <c>Areas/Domain/RiderTrips/RiderTripRules</c>.
    /// </summary>
    DepartureTooSoon = 502,

    /// <summary>Joining would take the posting past the seats any one car can carry.</summary>
    RideRequestSeatsExceeded = 503,

    /// <summary>
    /// The joining rider's own conditions would exclude somebody already
    /// holding a seat on this posting.
    /// </summary>
    RideRequestConditionsConflict = 504,

    /// <summary>The rider holds no seat on this posting, so there is nothing to leave.</summary>
    RideRequestNotJoined = 505,

    /// <summary>The rider is already a participant of this request.</summary>
    AlreadyJoined = 506,

    /// <summary>
    /// The driver is on this request as a rider. Nobody drives themselves, and
    /// the board already hides these — but a board is a cache, and this is the
    /// call that would actually commit them to both sides of the same journey.
    /// </summary>
    CannotServeOwnRequest = 507,

    /// <summary>The driver has no live interest on this request to withdraw.</summary>
    DriverInterestNotFound = 508,

    /// <summary>The driver did not agree that the trip is shared and its free seats stay on sale.</summary>
    SharedTermsNotAccepted = 509,

    /// <summary>
    /// The driver's reliability record has paused instant requests for now.
    /// Scheduled work is still open to them.
    /// </summary>
    DriverSuspended = 510,

    /// <summary>Riders may not pick an offer here — selection is immediate, or the setting is off.</summary>
    OfferChoiceNotAvailable = 511,

    /// <summary>The offer was withdrawn, or another driver was already chosen.</summary>
    OfferNotAvailable = 512,

    /// <summary>The seats a driver offered are fewer than the riders already need, or more than the car has.</summary>
    InvalidSeatsOffered = 513,

    DemandAlertNotFound = 520,

    // Trip safety and reliability
    /// <summary>Cancelling a trip riders depend on needs a reason.</summary>
    CancelReasonRequired = 320,

    /// <summary>The code the driver entered is not this rider's boarding code.</summary>
    BoardingCodeInvalid = 411,

    /// <summary>
    /// Boarding everyone at once would skip the boarding codes. Board each
    /// rider with their code instead.
    /// </summary>
    BoardingCodeRequired = 412,

    /// <summary>The shared trip link has been revoked or has expired.</summary>
    ShareLinkNotFound = 413,

    SafetyIncidentNotFound = 800,

    // Schedules
    ScheduleNotFound = 550,

    /// <summary>The recurrence describes no dates — no weekday chosen, or the window is empty.</summary>
    ScheduleHasNoOccurrences = 551,

    // Ratings
    RatingNotAllowed = 600,
    AlreadyRated = 601,

    // Complaints / suggestions
    FeedbackNotFound = 700,

    /// <summary>The user already has as many open submissions as the desk will hold for one account.</summary>
    TooManyOpenFeedback = 701,
}
