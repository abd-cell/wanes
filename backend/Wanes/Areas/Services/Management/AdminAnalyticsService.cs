using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Audit;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Logging;
using Wanes.Areas.Domain.Notifications;
using Wanes.Areas.Domain.Ratings;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Management.Models;
using Wanes.Areas.Services.Management.Models.Analytics;
using Wanes.DataAccess.Repositories;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Models.Base;

namespace Wanes.Areas.Services.Management;

/// <summary>
/// Aggregates every domain table into the numbers and series the CMS dashboard
/// draws. Read-only: nothing here writes, so there is no unit of work and no audit
/// entry. Soft-deleted rows are excluded by the repository default query.
/// </summary>
public class AdminAnalyticsService : IAdminAnalyticsService
{
    private const int DefaultDays = 30;
    private const int MaxDays = 365;
    private const int TopN = 8;
    private const int SlowCallMs = 1000;
    private const int LowRatingStars = 3;

    private readonly IRepository<User> userRepository;
    private readonly IRepository<UserLogin> userLoginRepository;
    private readonly IRepository<SavedPlace> savedPlaceRepository;
    private readonly IRepository<Vehicle> vehicleRepository;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<TripStatusHistory> tripHistoryRepository;
    private readonly IRepository<RideRequest> requestRepository;
    private readonly IRepository<Booking> bookingRepository;
    private readonly IRepository<Rating> ratingRepository;
    private readonly IRepository<UserNotification> notificationRepository;
    private readonly IRepository<ApiLog> apiLogRepository;
    private readonly IRepository<AuditLog> auditLogRepository;

    public AdminAnalyticsService(
        IRepository<User> userRepository,
        IRepository<UserLogin> userLoginRepository,
        IRepository<SavedPlace> savedPlaceRepository,
        IRepository<Vehicle> vehicleRepository,
        IRepository<Trip> tripRepository,
        IRepository<TripStatusHistory> tripHistoryRepository,
        IRepository<RideRequest> requestRepository,
        IRepository<Booking> bookingRepository,
        IRepository<Rating> ratingRepository,
        IRepository<UserNotification> notificationRepository,
        IRepository<ApiLog> apiLogRepository,
        IRepository<AuditLog> auditLogRepository)
    {
        this.userRepository = userRepository;
        this.userLoginRepository = userLoginRepository;
        this.savedPlaceRepository = savedPlaceRepository;
        this.vehicleRepository = vehicleRepository;
        this.tripRepository = tripRepository;
        this.tripHistoryRepository = tripHistoryRepository;
        this.requestRepository = requestRepository;
        this.bookingRepository = bookingRepository;
        this.ratingRepository = ratingRepository;
        this.notificationRepository = notificationRepository;
        this.apiLogRepository = apiLogRepository;
        this.auditLogRepository = auditLogRepository;
    }

    public async Task<BaseResponse<OverviewOutput>> GetOverview(int days)
    {
        var w = Window(days);
        var prevFrom = w.From.AddDays(-w.Days);

        var output = new OverviewOutput { RangeDays = w.Days, From = w.From, To = w.To.AddDays(-1) };

        // people
        var byDriverStatus = await CountBy(userRepository.Query(), u => (int)u.DriverStatus);
        output.Users = await userRepository.Query().CountAsync();
        output.NewUsers = await InRange(userRepository.Query(), w).CountAsync();
        output.ActiveUsers = await userRepository.Query()
            .CountAsync(u => u.LastSeenAt != null && u.LastSeenAt >= w.From);
        output.Riders = await userRepository.Query().CountAsync(u => u.IsRider);
        output.Drivers = await userRepository.Query().CountAsync(u => u.IsDriver);
        output.VerifiedDrivers = byDriverStatus.GetValueOrDefault((int)DriverStatus.Verified);
        output.PendingDrivers = byDriverStatus.GetValueOrDefault((int)DriverStatus.Pending);
        output.OnlineDrivers = await userRepository.Query().CountAsync(u => u.IsDriver && u.IsOnline);
        output.DisabledUsers = await userRepository.Query().CountAsync(u => u.IsDisabled);

        // fleet
        output.Vehicles = await vehicleRepository.Query().CountAsync();
        output.VehicleSeats = await Sum(vehicleRepository.Query(), v => v.SeatCapacity);

        // supply
        var byTripStatus = await CountBy(tripRepository.Query(), t => (int)t.Status);
        output.Trips = byTripStatus.Values.Sum();
        output.NewTrips = await InRange(tripRepository.Query(), w).CountAsync();
        output.ActiveTrips = byTripStatus.GetValueOrDefault((int)TripStatus.Posted)
            + byTripStatus.GetValueOrDefault((int)TripStatus.Full)
            + byTripStatus.GetValueOrDefault((int)TripStatus.Active);
        output.CompletedTrips = byTripStatus.GetValueOrDefault((int)TripStatus.Completed);
        output.CancelledTrips = byTripStatus.GetValueOrDefault((int)TripStatus.Cancelled);
        output.SeatsOffered = await Sum(tripRepository.Query(), t => t.SeatsTotal);
        var seatsLeft = await Sum(tripRepository.Query(), t => t.SeatsLeft);
        output.SeatsTaken = Math.Max(0, output.SeatsOffered - seatsLeft);
        output.SeatFillRate = Ratio(output.SeatsTaken, output.SeatsOffered);

        // demand
        var byBookingStatus = await CountBy(bookingRepository.Query(), b => (int)b.Status);
        output.Bookings = byBookingStatus.Values.Sum();
        output.NewBookings = await InRange(bookingRepository.Query(), w).CountAsync();
        output.CompletedBookings = byBookingStatus.GetValueOrDefault((int)BookingStatus.Completed);
        output.CancelledBookings = byBookingStatus.GetValueOrDefault((int)BookingStatus.Cancelled);
        output.BookingCancelRate = Ratio(output.CancelledBookings, output.Bookings);
        output.AvgSeatsPerBooking = Math.Round(await Average(bookingRepository.Query(), b => (double?)b.Seats), 2);

        // Demand, read straight off its own table.
        //
        // This used to be inferred from the status log, because demand was a
        // Trips row and the only record of it having been one was an
        // AwaitingDriver history entry. Now that demand is its own object the
        // question is literal, and every figure below is a count rather than a
        // reconstruction.
        var demand = requestRepository.Query();

        output.RiderTrips = await demand.CountAsync();
        output.NewRiderTrips = await InRange(demand, w).CountAsync();
        output.OpenRiderTrips = await demand.CountAsync(r => r.Status == RideRequestStatus.Open);

        // Matched with a driver, versus reached its departure with nobody
        // driving. Both are terminal facts about the same rows.
        output.ClaimedRiderTrips = await demand.CountAsync(r => r.Status == RideRequestStatus.Matched);
        output.ExpiredRiderTrips = await demand.CountAsync(r => r.Status == RideRequestStatus.Expired);
        output.MatchRate = Ratio(output.ClaimedRiderTrips, output.RiderTrips);

        // quality
        output.Ratings = await ratingRepository.Query().CountAsync();
        output.AvgRating = Math.Round(await Average(ratingRepository.Query(), r => (double?)r.Stars), 2);
        output.LowRatings = await ratingRepository.Query().CountAsync(r => r.Stars <= LowRatingStars);

        // engagement
        output.Notifications = await notificationRepository.Query().CountAsync();
        output.UnreadNotifications = await notificationRepository.Query().CountAsync(n => !n.IsRead);
        output.SavedPlaces = await savedPlaceRepository.Query().CountAsync();
        output.ActiveSessions = await userLoginRepository.Query().CountAsync();
        output.NewLogins = await InRange(userLoginRepository.Query(), w).CountAsync();

        // platform
        var apiInRange = InRange(apiLogRepository.Query(), w);
        output.ApiCalls = await apiInRange.CountAsync();
        output.ApiErrors = await apiInRange.CountAsync(l => l.StatusCode >= 400);
        output.ApiErrorRate = Ratio(output.ApiErrors, output.ApiCalls);
        output.AvgResponseMs = Math.Round(await Average(apiInRange, l => (double?)l.DurationMs), 1);
        output.AuditEvents = await InRange(auditLogRepository.Query(), w).CountAsync();

        // period over period
        output.UsersTrend = Trend(output.NewUsers,
            await InPrevRange(userRepository.Query(), prevFrom, w.From).CountAsync());
        output.TripsTrend = Trend(output.NewTrips,
            await InPrevRange(tripRepository.Query(), prevFrom, w.From).CountAsync());
        output.BookingsTrend = Trend(output.NewBookings,
            await InPrevRange(bookingRepository.Query(), prevFrom, w.From).CountAsync());
        output.RiderTripsTrend = Trend(output.NewRiderTrips,
            await InPrevRange(requestRepository.Query(), prevFrom, w.From).CountAsync());

        return new BaseResponse<OverviewOutput>(output);
    }

    public async Task<BaseResponse<TimeSeriesOutput>> GetTimeSeries(int days)
    {
        var w = Window(days);

        var output = new TimeSeriesOutput
        {
            From = w.From,
            To = w.To.AddDays(-1),
            NewUsers = await Daily(InRange(userRepository.Query(), w), w),
            NewTrips = await Daily(InRange(tripRepository.Query(), w), w),
            NewBookings = await Daily(InRange(bookingRepository.Query(), w), w),
            NewRiderTrips = await Daily(InRange(requestRepository.Query(), w), w),
            Logins = await Daily(InRange(userLoginRepository.Query(), w), w),
            CompletedTrips = await Daily(
                InRange(tripHistoryRepository.Query(), w).Where(h => h.Status == TripStatus.Completed), w),
            CancelledTrips = await Daily(
                InRange(tripHistoryRepository.Query(), w).Where(h => h.Status == TripStatus.Cancelled), w),
            SeatsBooked = await DailySeats(InRange(bookingRepository.Query(), w), w),
        };

        return new BaseResponse<TimeSeriesOutput>(output);
    }

    public async Task<BaseResponse<BreakdownsOutput>> GetBreakdowns(int days)
    {
        var w = Window(days);

        var output = new BreakdownsOutput
        {
            TripsByStatus = Complete<TripStatus>(await CountBy(tripRepository.Query(), t => (int)t.Status)),
            BookingsByStatus = Complete<BookingStatus>(await CountBy(bookingRepository.Query(), b => (int)b.Status)),
            // Demand by its own lifecycle now, not by a trip's. The two never
            // lined up — "open" and "expired" have no trip status that means
            // them — and the chart was showing Cancelled for both a request
            // nobody took and one whose riders withdrew.
            RiderTripsByStatus = Complete<RideRequestStatus>(
                await CountBy(requestRepository.Query(), r => (int)r.Status)),
            UsersByDriverStatus = Complete<DriverStatus>(await CountBy(userRepository.Query(), u => (int)u.DriverStatus)),
            UsersByLanguage = Complete<Language>(await CountBy(userRepository.Query(), u => (int)u.Language)),
            UsersByGender = Complete<Gender>(await CountBy(userRepository.Query(), u => (int)u.Gender)),
            NotificationsByType = Complete<NotificationType>(await CountBy(notificationRepository.Query(), n => (int)n.Type)),
            SessionsByDevice = Complete<DeviceType>(await CountBy(userLoginRepository.Query(), s => (int)s.DeviceType)),
            PlacesByLabel = Complete<SavedPlaceLabel>(await CountBy(savedPlaceRepository.Query(), p => (int)p.Label)),
        };

        // stars are a 1..5 scale, not an enum
        var stars = await CountBy(ratingRepository.Query(), r => r.Stars);
        output.RatingsByStars = Enumerable.Range(1, 5)
            .Select(s => new MetricPoint { Key = s, Label = s.ToString(), Value = stars.GetValueOrDefault(s) })
            .ToList();

        // Hour-of-day and weekday are grouped in memory: the window bounds the row
        // count, and DATEPART translation is provider-specific.
        var bookingTimes = await InRange(bookingRepository.Query(), w).Select(b => b.CreationDate).ToListAsync();
        var requestTimes = await InRange(requestRepository.Query(), w)
            .Select(r => r.CreationDate).ToListAsync();
        var demand = bookingTimes.Concat(requestTimes)
            .GroupBy(d => d.Hour)
            .ToDictionary(g => g.Key, g => g.Count());
        output.DemandByHour = Enumerable.Range(0, 24)
            .Select(h => new MetricPoint { Key = h, Label = h.ToString("00"), Value = demand.GetValueOrDefault(h) })
            .ToList();

        var departures = await InRange(tripRepository.Query(), w).Select(t => t.DepartAt).ToListAsync();
        var weekday = departures.GroupBy(d => (int)d.DayOfWeek).ToDictionary(g => g.Key, g => g.Count());
        output.TripsByWeekday = Enumerable.Range(0, 7)
            .Select(i => new MetricPoint
            {
                Key = i,
                Label = ((DayOfWeek)i).ToString(),
                Value = weekday.GetValueOrDefault(i),
            })
            .ToList();

        return new BaseResponse<BreakdownsOutput>(output);
    }

    public async Task<BaseResponse<OperationsOutput>> GetOperations(int days)
    {
        var w = Window(days);
        var logs = InRange(apiLogRepository.Query(), w);

        var output = new OperationsOutput
        {
            TotalCalls = await logs.CountAsync(),
            Ok2xx = await logs.CountAsync(l => l.StatusCode >= 200 && l.StatusCode < 300),
            Redirect3xx = await logs.CountAsync(l => l.StatusCode >= 300 && l.StatusCode < 400),
            ClientError4xx = await logs.CountAsync(l => l.StatusCode >= 400 && l.StatusCode < 500),
            ServerError5xx = await logs.CountAsync(l => l.StatusCode >= 500),
            SlowCalls = await logs.CountAsync(l => l.DurationMs > SlowCallMs),
            UnauthenticatedCalls = await logs.CountAsync(l => l.ActorUserId == null),
            AvgDurationMs = Math.Round(await Average(logs, l => (double?)l.DurationMs), 1),
            MaxDurationMs = await Max(logs, l => (long?)l.DurationMs),
            CallsByDay = await Daily(logs, w),
            ErrorsByDay = await Daily(logs.Where(l => l.StatusCode >= 400), w),
            AuditByDay = await Daily(InRange(auditLogRepository.Query(), w), w),
        };

        var endpoints = await logs
            .GroupBy(l => new { l.Method, l.Path })
            .Select(g => new EndpointStat
            {
                Method = g.Key.Method,
                Path = g.Key.Path,
                Calls = g.Count(),
                AvgDurationMs = g.Average(l => (double)l.DurationMs),
                MaxDurationMs = g.Max(l => l.DurationMs),
                Errors = g.Count(l => l.StatusCode >= 400),
            })
            .ToListAsync();

        foreach (var endpoint in endpoints) endpoint.AvgDurationMs = Math.Round(endpoint.AvgDurationMs, 1);
        output.TopEndpoints = endpoints.OrderByDescending(e => e.Calls).Take(TopN).ToList();
        output.SlowestEndpoints = endpoints.OrderByDescending(e => e.AvgDurationMs).Take(TopN).ToList();

        output.TopAuditActions = await InRange(auditLogRepository.Query(), w)
            .GroupBy(a => a.Action)
            .Select(g => new LeaderRow { Label = g.Key, Value = g.Count() })
            .OrderByDescending(r => r.Value)
            .Take(TopN)
            .ToListAsync();

        return new BaseResponse<OperationsOutput>(output);
    }

    public async Task<BaseResponse<LeaderboardsOutput>> GetLeaderboards(int days)
    {
        var w = Window(days);
        var output = new LeaderboardsOutput();

        // Trips nobody is driving have no driver to credit.
        var driverTrips = await InRange(tripRepository.Query().Where(t => t.DriverId != null), w)
            .GroupBy(t => t.DriverId!.Value)
            .Select(g => new { UserId = g.Key, Trips = g.Count(), Seats = g.Sum(t => t.SeatsTotal) })
            .OrderByDescending(x => x.Trips)
            .Take(TopN)
            .ToListAsync();
        var driverNames = await NameMap(driverTrips.Select(x => x.UserId));
        output.TopDrivers = driverTrips
            .Select(x => new LeaderRow
            {
                Id = x.UserId,
                Label = Display(driverNames.GetValueOrDefault(x.UserId), x.UserId),
                Value = x.Trips,
                Secondary = x.Seats,
            })
            .ToList();

        var riderBookings = await InRange(bookingRepository.Query(), w)
            .GroupBy(b => b.RiderId)
            .Select(g => new { UserId = g.Key, Bookings = g.Count(), Seats = g.Sum(b => b.Seats) })
            .OrderByDescending(x => x.Bookings)
            .Take(TopN)
            .ToListAsync();
        var riderNames = await NameMap(riderBookings.Select(x => x.UserId));
        output.TopRiders = riderBookings
            .Select(x => new LeaderRow
            {
                Id = x.UserId,
                Label = Display(riderNames.GetValueOrDefault(x.UserId), x.UserId),
                Value = x.Bookings,
                Secondary = x.Seats,
            })
            .ToList();

        var trips = InRange(tripRepository.Query(), w)
            .Where(t => t.OriginAddress != "" && t.DestinationAddress != "");

        output.TopRoutes = await trips
            .GroupBy(t => new { t.OriginAddress, t.DestinationAddress })
            .Select(g => new LeaderRow
            {
                Label = g.Key.OriginAddress,
                Sublabel = g.Key.DestinationAddress,
                Value = g.Count(),
            })
            .OrderByDescending(r => r.Value)
            .Take(TopN)
            .ToListAsync();

        output.TopOrigins = await trips
            .GroupBy(t => t.OriginAddress)
            .Select(g => new LeaderRow { Label = g.Key, Value = g.Count() })
            .OrderByDescending(r => r.Value)
            .Take(TopN)
            .ToListAsync();

        output.TopDestinations = await trips
            .GroupBy(t => t.DestinationAddress)
            .Select(g => new LeaderRow { Label = g.Key, Value = g.Count() })
            .OrderByDescending(r => r.Value)
            .Take(TopN)
            .ToListAsync();

        var ratedDrivers = await userRepository.Query()
            .Where(u => u.IsDriver && u.RatingCount > 0)
            .OrderByDescending(u => u.RatingAvg).ThenByDescending(u => u.RatingCount)
            .Take(TopN)
            .ToListAsync();

        output.TopRatedDrivers = ratedDrivers
            .Select(u => new LeaderRow
            {
                Id = u.Id,
                Label = Display(AdminLabels.ForUser(u), u.Id),
                Value = u.RatingCount,
                Secondary = u.RatingAvg,
            })
            .ToList();

        return new BaseResponse<LeaderboardsOutput>(output);
    }

    // ── window + query helpers ──

    /// <summary>Rolling window ending at the end of today. <c>To</c> is exclusive.</summary>
    private readonly record struct Range(DateTime From, DateTime To, int Days);

    private static Range Window(int days)
    {
        var span = days is < 1 or > MaxDays ? DefaultDays : days;
        var to = DateTime.UtcNow.Date.AddDays(1);
        return new Range(to.AddDays(-span), to, span);
    }

    private static IQueryable<T> InRange<T>(IQueryable<T> query, Range w) where T : BaseEntity =>
        query.Where(x => x.CreationDate >= w.From && x.CreationDate < w.To);

    private static IQueryable<T> InPrevRange<T>(IQueryable<T> query, DateTime from, DateTime to)
        where T : BaseEntity =>
        query.Where(x => x.CreationDate >= from && x.CreationDate < to);

    private static async Task<Dictionary<int, int>> CountBy<T>(IQueryable<T> query, Expression<Func<T, int>> key) =>
        await query.GroupBy(key)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

    private static async Task<int> Sum<T>(IQueryable<T> query, Expression<Func<T, int?>> selector) =>
        await query.SumAsync(selector) ?? 0;

    private static async Task<double> Average<T>(IQueryable<T> query, Expression<Func<T, double?>> selector) =>
        await query.AverageAsync(selector) ?? 0;

    private static async Task<long> Max<T>(IQueryable<T> query, Expression<Func<T, long?>> selector) =>
        await query.MaxAsync(selector) ?? 0;

    /// <summary>Daily row counts across the window, zero-filled.</summary>
    private static async Task<List<SeriesPoint>> Daily<T>(IQueryable<T> query, Range w) where T : BaseEntity
    {
        var buckets = await query
            .GroupBy(x => x.CreationDate.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Day, x => x.Count);
        return Fill(buckets, w);
    }

    /// <summary>Daily seat totals across the window, zero-filled.</summary>
    private static async Task<List<SeriesPoint>> DailySeats(IQueryable<Booking> query, Range w)
    {
        var buckets = await query
            .GroupBy(b => b.CreationDate.Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(b => b.Seats) })
            .ToDictionaryAsync(x => x.Day, x => x.Total);
        return Fill(buckets, w);
    }

    private static List<SeriesPoint> Fill(Dictionary<DateTime, int> buckets, Range w) =>
        Enumerable.Range(0, w.Days)
            .Select(offset => w.From.AddDays(offset))
            .Select(day => new SeriesPoint { Date = day, Value = buckets.GetValueOrDefault(day) })
            .ToList();

    /// <summary>Seeded accounts can have no name yet — fall back to the id rather than a blank row.</summary>
    private static string Display(string? name, int id) =>
        string.IsNullOrWhiteSpace(name) ? "#" + id : name;

    private async Task<Dictionary<int, string>> NameMap(IEnumerable<int> ids)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0) return [];
        var users = await userRepository.Query().Where(u => wanted.Contains(u.Id)).ToListAsync();
        return users.ToDictionary(u => u.Id, u => AdminLabels.ForUser(u) ?? string.Empty);
    }

    /// <summary>Every value of the enum, zero-filled, so chart legends stay stable.</summary>
    private static List<MetricPoint> Complete<TEnum>(Dictionary<int, int> counts) where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>()
            .Select(value => Convert.ToInt32(value))
            .Distinct()
            .Select(key => new MetricPoint
            {
                Key = key,
                Label = Enum.GetName(typeof(TEnum), key) ?? key.ToString(),
                Value = counts.GetValueOrDefault(key),
            })
            .ToList();

    private static double Ratio(int part, int whole) =>
        whole <= 0 ? 0 : Math.Round((double)part / whole, 4);

    /// <summary>Null when the previous window was empty and a percentage would mislead.</summary>
    private static double? Trend(int current, int previous) =>
        previous <= 0 ? null : Math.Round((double)(current - previous) / previous, 4);
}
