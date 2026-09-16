using System.Globalization;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;

namespace Wanes.Areas.Services.Management.Models;

/// <summary>
/// Single source of truth for the human-readable labels the CMS shows in place of
/// raw foreign keys — a rider's name instead of "13", a trip's route instead of "1".
/// Both the admin row DTOs and the lookup pickers build their text from here so a
/// record reads the same in a table cell and in the picker that selects it.
/// The <c>*Detail</c> overloads carry the secondary line (phone, time, id).
/// </summary>
public static class AdminLabels
{
    private const string TimeFormat = "d MMM HH:mm";

    /// <summary>
    /// "+962790000000 · Ahmad Ali". The phone is the verified identity so it leads,
    /// and the name follows it — an admin reading a booking or a rating should not
    /// have to look a number up to know who it belongs to. Accounts with no name
    /// yet show the phone alone.
    /// </summary>
    public static string? ForUser(User? user)
    {
        if (user == null) return null;
        var name = NameOf(user);
        if (string.IsNullOrWhiteSpace(user.Phone)) return string.IsNullOrWhiteSpace(name) ? Ref(user.Id) : name;
        return string.IsNullOrWhiteSpace(name) ? user.Phone : Join(user.Phone, name);
    }

    /// <summary>"sam@wanes.app · #13" — the phone already rides in the label.</summary>
    public static string? ForUserDetail(User? user) =>
        user == null ? null : Join(user.Email, Ref(user.Id));

    /// <summary>
    /// Just the name, for screens that already show the phone in its own column.
    /// Falls back to the display name, then to the record reference.
    /// </summary>
    public static string ForUserName(User? user) =>
        user == null ? string.Empty
        : NameOf(user) is { Length: > 0 } name ? name
        : Ref(user.Id);

    /// <summary>Full name, falling back to the display name. Empty when neither is set.</summary>
    private static string NameOf(User user)
    {
        var full = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(full) ? user.DisplayName?.Trim() ?? string.Empty : full;
    }

    /// <summary>"Amman → Zarqa".</summary>
    public static string? ForTrip(Trip? trip) =>
        trip == null ? null : Route(trip.OriginAddress, trip.DestinationAddress, trip.Id);

    /// <summary>"3 Sep 08:00 · Ahmad Ali · #12".</summary>
    public static string? ForTripDetail(Trip? trip) =>
        trip == null ? null : Join(Time(trip.DepartAt), ForUser(trip.Driver), Ref(trip.Id));

    /// <summary>"Ahmad Ali · Amman → Zarqa" — the rider and where they are going.</summary>
    public static string? ForBooking(Booking? booking) =>
        booking == null ? null
        : Join(ForUser(booking.Rider), ForTrip(booking.Trip)) is { Length: > 0 } text ? text
        : Ref(booking.Id);

    /// <summary>"2 seats · 3 Sep 08:00 · #7".</summary>
    public static string? ForBookingDetail(Booking? booking) =>
        booking == null ? null
        : Join(Seats(booking.Seats), booking.Trip == null ? null : Time(booking.Trip.DepartAt), Ref(booking.Id));

    /// <summary>"Toyota Corolla · 12-3456".</summary>
    public static string? ForVehicle(Vehicle? vehicle) =>
        vehicle == null ? null : Join($"{vehicle.Make} {vehicle.Model}".Trim(), vehicle.Plate);

    /// <summary>"Ahmad Ali · 4 seats · #3".</summary>
    public static string? ForVehicleDetail(Vehicle? vehicle) =>
        vehicle == null ? null : Join(ForUser(vehicle.User), Seats(vehicle.SeatCapacity), Ref(vehicle.Id));

    /// <summary>"Amman → Zarqa", or "#12" when neither address was captured.</summary>
    private static string Route(string? origin, string? destination, int id)
    {
        var from = origin?.Trim();
        var to = destination?.Trim();
        if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to)) return Ref(id);
        return $"{(string.IsNullOrWhiteSpace(from) ? "?" : from)} → {(string.IsNullOrWhiteSpace(to) ? "?" : to)}";
    }

    private static string Time(DateTime value) => value.ToString(TimeFormat, CultureInfo.InvariantCulture);

    private static string Seats(int seats) => seats == 1 ? "1 seat" : $"{seats} seats";

    private static string Ref(int id) => $"#{id}";

    private static string Join(params string?[] parts) =>
        string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
