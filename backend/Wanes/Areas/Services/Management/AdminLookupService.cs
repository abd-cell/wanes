using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Management.Models;
using Wanes.DataAccess.Repositories;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;

namespace Wanes.Areas.Services.Management;

/// <summary>
/// Backs the CMS reference pickers: instead of typing a foreign key, the admin
/// searches records by their human details and picks one. Every type returns the
/// same <see cref="LookupRow"/> shape so a single picker component serves them all.
/// </summary>
public class AdminLookupService : IAdminLookupService
{
    private readonly IRepository<User> userRepository;
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<Booking> bookingRepository;

    public AdminLookupService(
        IRepository<User> userRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<Trip> tripRepository,
        IRepository<Booking> bookingRepository)
    {
        this.userRepository = userRepository;
        this.vehicleRepository = vehicleRepository;
        this.tripRepository = tripRepository;
        this.bookingRepository = bookingRepository;
    }

    public async Task<BaseResponse<PageOutput<LookupRow>>> Search(string type, PageInput page)
    {
        var term = string.IsNullOrWhiteSpace(page.Search) ? null : page.Search.Trim();
        var id = int.TryParse(term, out var parsed) ? parsed : (int?)null;

        return type?.ToLowerInvariant() switch
        {
            "users" => await SearchUsers(page, term, id, null),
            "drivers" => await SearchUsers(page, term, id, u => u.IsDriver),
            "riders" => await SearchUsers(page, term, id, u => u.IsRider),
            "vehicles" => await SearchVehicles(page, term, id),
            "trips" => await SearchTrips(page, term, id),
            "bookings" => await SearchBookings(page, term, id),
            _ => new BaseResponse<PageOutput<LookupRow>>(default, ErrorCode.NotFound, $"Unknown lookup type: {type}."),
        };
    }

    public async Task<BaseResponse<LookupRow>> Get(string type, int id)
    {
        LookupRow? row = type?.ToLowerInvariant() switch
        {
            "users" or "drivers" or "riders" => Row(await userRepository.GetByIdAsync(id)),
            "vehicles" => Row(await VehicleQuery().FirstOrDefaultAsync(v => v.Id == id)),
            "trips" => Row(await TripQuery().FirstOrDefaultAsync(t => t.Id == id)),
            "bookings" => Row(await BookingQuery().FirstOrDefaultAsync(b => b.Id == id)),
            _ => null,
        };

        return row == null
            ? new BaseResponse<LookupRow>(default, ErrorCode.NotFound)
            : new BaseResponse<LookupRow>(row);
    }

    // ── per-type searches ──
    private async Task<BaseResponse<PageOutput<LookupRow>>> SearchUsers(
        PageInput page, string? term, int? id, Expression<Func<User, bool>>? scope)
    {
        var query = userRepository.Query();
        if (scope != null) query = query.Where(scope);
        if (term != null)
            query = query.Where(u =>
                u.Phone.Contains(term) ||
                u.FirstName.Contains(term) ||
                u.LastName.Contains(term) ||
                (u.DisplayName != null && u.DisplayName.Contains(term)) ||
                (u.Email != null && u.Email.Contains(term)) ||
                u.Id == id);

        return await Page(query.OrderByDescending(u => u.Id), page, Row);
    }

    private async Task<BaseResponse<PageOutput<LookupRow>>> SearchVehicles(PageInput page, string? term, int? id)
    {
        var query = VehicleQuery();
        if (term != null)
            query = query.Where(v =>
                v.Make.Contains(term) ||
                v.Model.Contains(term) ||
                v.Plate.Contains(term) ||
                (v.User != null && (v.User.FirstName.Contains(term) || v.User.LastName.Contains(term))) ||
                v.Id == id);

        return await Page(query.OrderByDescending(v => v.Id), page, Row);
    }

    private async Task<BaseResponse<PageOutput<LookupRow>>> SearchTrips(PageInput page, string? term, int? id)
    {
        var query = TripQuery();
        if (term != null)
            query = query.Where(t =>
                t.OriginAddress.Contains(term) ||
                t.DestinationAddress.Contains(term) ||
                (t.Driver != null && (t.Driver.FirstName.Contains(term) || t.Driver.LastName.Contains(term))) ||
                t.Id == id);

        return await Page(query.OrderByDescending(t => t.Id), page, Row);
    }

    private async Task<BaseResponse<PageOutput<LookupRow>>> SearchBookings(PageInput page, string? term, int? id)
    {
        var query = BookingQuery();
        if (term != null)
            query = query.Where(b =>
                (b.Rider != null && (b.Rider.FirstName.Contains(term) || b.Rider.LastName.Contains(term))) ||
                (b.Trip != null && (b.Trip.OriginAddress.Contains(term) || b.Trip.DestinationAddress.Contains(term))) ||
                b.Id == id);

        return await Page(query.OrderByDescending(b => b.Id), page, Row);
    }

    // ── shared shaping (labels are built in memory; they do not translate to SQL) ──
    private static async Task<BaseResponse<PageOutput<LookupRow>>> Page<T>(
        IQueryable<T> query, PageInput page, Func<T, LookupRow?> toRow)
    {
        var total = await query.CountAsync();
        var items = await query.Paginate(page).ToListAsync();
        var rows = items.Select(toRow).OfType<LookupRow>().ToList();

        return new BaseResponse<PageOutput<LookupRow>>(new PageOutput<LookupRow> { TotalRows = total, Data = rows });
    }

    private IQueryable<Vehicle> VehicleQuery() => vehicleRepository.Query().Include(v => v.User);

    private IQueryable<Trip> TripQuery() => tripRepository.Query().Include(t => t.Driver);

    private IQueryable<Booking> BookingQuery() =>
        bookingRepository.Query().Include(b => b.Rider).Include(b => b.Trip);

    private static LookupRow? Row(User? user) =>
        user == null ? null : new LookupRow(user.Id, AdminLabels.ForUser(user), AdminLabels.ForUserDetail(user));

    private static LookupRow? Row(Vehicle? vehicle) =>
        vehicle == null ? null : new LookupRow(vehicle.Id, AdminLabels.ForVehicle(vehicle), AdminLabels.ForVehicleDetail(vehicle));

    private static LookupRow? Row(Trip? trip) =>
        trip == null ? null : new LookupRow(trip.Id, AdminLabels.ForTrip(trip), AdminLabels.ForTripDetail(trip));

    private static LookupRow? Row(Booking? booking) =>
        booking == null ? null : new LookupRow(booking.Id, AdminLabels.ForBooking(booking), AdminLabels.ForBookingDetail(booking));

}
