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

    // Trips
    TripNotFound = 300,
    TripNotBookable = 301,
    DepartureMustBeFuture = 302,
    SeatsExceedCapacity = 303,
    OriginEqualsDestination = 304,
    GeocodingFailed = 305,
    TripNotEditable = 306,

    // Bookings
    BookingNotFound = 400,
    NoSeatsLeft = 401,
    CannotBookOwnTrip = 402,
    AlreadyBooked = 403,

    // Ride requests
    RequestNotFound = 500,
    RequestNotOpen = 501,

    // Ratings
    RatingNotAllowed = 600,
    AlreadyRated = 601,
}
