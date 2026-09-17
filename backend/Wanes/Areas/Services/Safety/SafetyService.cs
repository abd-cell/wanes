using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Notifications;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Notifications.Sms;
using Wanes.Shareds.Security;

namespace Wanes.Areas.Services.Safety;

// ── Models ───────────────────────────────────────────────────────────────────

public class RaiseSafetyInput
{
    [EnumDataType(typeof(SafetyIncidentKind))]
    public SafetyIncidentKind Kind { get; set; } = SafetyIncidentKind.Sos;

    public int? TripId { get; set; }
    public int? BookingId { get; set; }

    [Range(-90, 90)]
    public double? Lat { get; set; }

    [Range(-180, 180)]
    public double? Lng { get; set; }

    [StringLength(1000)]
    public string? Note { get; set; }
}

public class SafetyIncidentOutput
{
    public int Id { get; set; }
    public int ReporterId { get; set; }
    public string? ReporterName { get; set; }
    public string? ReporterPhone { get; set; }
    public SafetyIncidentKind Kind { get; set; }
    public SafetyIncidentStatus Status { get; set; }
    public int? TripId { get; set; }
    public int? BookingId { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? Note { get; set; }
    public bool EmergencyContactNotified { get; set; }
    public string? AdminNote { get; set; }
    public DateTime? HandledAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>What the app dials — sent back so the button and the setting never disagree.</summary>
    public string EmergencyNumber { get; set; } = string.Empty;

    public SafetyIncidentOutput() { }

    public SafetyIncidentOutput(SafetyIncident e)
    {
        Id = e.Id;
        ReporterId = e.ReporterId;
        ReporterName = e.Reporter == null ? null : $"{e.Reporter.FirstName} {e.Reporter.LastName}".Trim();
        ReporterPhone = e.Reporter?.Phone;
        Kind = e.Kind;
        Status = e.Status;
        TripId = e.TripId;
        BookingId = e.BookingId;
        Lat = e.Location?.Y;
        Lng = e.Location?.X;
        Note = e.Note;
        EmergencyContactNotified = e.EmergencyContactNotified;
        AdminNote = e.AdminNote;
        HandledAt = e.HandledAt;
        CreatedAt = e.CreationDate;
    }
}

public class UpdateSafetyIncidentInput
{
    [EnumDataType(typeof(SafetyIncidentStatus))]
    public SafetyIncidentStatus Status { get; set; }

    [StringLength(1000)]
    public string? AdminNote { get; set; }
}

public class ShareLinkOutput
{
    public string Token { get; set; } = string.Empty;

    /// <summary>The full link when the admin has set a public address; null otherwise.</summary>
    public string? Url { get; set; }
}

/// <summary>
/// What someone holding a trip link may see: enough to know where their person
/// is and who is driving them, and nothing that identifies the other riders.
/// </summary>
public class SharedTripOutput
{
    public string RiderFirstName { get; set; } = string.Empty;
    public string OriginAddress { get; set; } = string.Empty;
    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public double DestinationLat { get; set; }
    public double DestinationLng { get; set; }
    public DateTime DepartAt { get; set; }
    public BookingStatus BookingStatus { get; set; }
    public TripStatus TripStatus { get; set; }
    public string? DriverFirstName { get; set; }
    public double DriverRating { get; set; }
    public string? VehicleLabel { get; set; }
    public string? VehicleColor { get; set; }
    public string? VehiclePlate { get; set; }
    public double? DriverLat { get; set; }
    public double? DriverLng { get; set; }
    public DateTime? DriverLocationAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

[TransientInjectable]
public interface ISafetyService
{
    Task<BaseResponse<SafetyIncidentOutput>> Raise(RaiseSafetyInput input);

    Task<BaseResponse<ShareLinkOutput>> CreateShareLink(int bookingId);
    Task<BaseResponse> RevokeShareLink(int bookingId);

    /// <summary>Anonymous: the page a shared link opens.</summary>
    Task<BaseResponse<SharedTripOutput>> GetShared(string token);

    Task<BaseResponse<PageOutput<SafetyIncidentOutput>>> List(PageInput page, SafetyIncidentStatus? status);
    Task<BaseResponse<SafetyIncidentOutput>> Get(int id);
    Task<BaseResponse<SafetyIncidentOutput>> Update(int id, UpdateSafetyIncidentInput input);
}

/// <summary>
/// The emergency button, the "follow my trip" link, and the admin team's queue.
/// </summary>
public class SafetyService : ISafetyService
{
    /// <summary>How long a link keeps working after the ride ends.</summary>
    public static readonly TimeSpan LinkAfterRide = TimeSpan.FromHours(2);

    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IAuditService auditService;
    private readonly INotificationService notificationService;
    private readonly ISmsSender smsSender;
    private readonly IAppConfigurationService appConfigurationService;
    private readonly IRepository<SafetyIncident> incidentRepository;
    private readonly IRepository<Trip> tripRepository;
    private readonly IRepository<Booking> bookingRepository;
    private readonly IRepository<User> userRepository;
    private readonly IRepository<UserRole> roleRepository;

    public SafetyService(
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IAuditService auditService,
        INotificationService notificationService,
        ISmsSender smsSender,
        IAppConfigurationService appConfigurationService,
        IRepository<SafetyIncident> incidentRepository,
        IRepository<Trip> tripRepository,
        IRepository<Booking> bookingRepository,
        IRepository<User> userRepository,
        IRepository<UserRole> roleRepository)
    {
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.auditService = auditService;
        this.notificationService = notificationService;
        this.smsSender = smsSender;
        this.appConfigurationService = appConfigurationService;
        this.incidentRepository = incidentRepository;
        this.tripRepository = tripRepository;
        this.bookingRepository = bookingRepository;
        this.userRepository = userRepository;
        this.roleRepository = roleRepository;
    }

    public async Task<BaseResponse<SafetyIncidentOutput>> Raise(RaiseSafetyInput input)
    {
        var userId = securityManager.RequireUserId();
        var user = await userRepository.GetByIdAsync(userId);
        if (user == null) return new BaseResponse<SafetyIncidentOutput>(default, ErrorCode.NotFound);

        // Only somebody on the ride may attach it: a report is not a way to
        // read another person's trip.
        Booking? booking = null;
        if (input.BookingId is { } bookingId)
        {
            booking = bookingRepository.FirstOrDefault(b => b.Id == bookingId);
            if (booking == null) return new BaseResponse<SafetyIncidentOutput>(default, ErrorCode.BookingNotFound);
            input.TripId ??= booking.TripId;
        }
        Trip? trip = null;
        if (input.TripId is { } tripId)
        {
            trip = tripRepository.FirstOrDefault(t => t.Id == tripId);
            if (trip == null) return new BaseResponse<SafetyIncidentOutput>(default, ErrorCode.TripNotFound);
            var onIt = trip.DriverId == userId
                       || await bookingRepository.AnyAsync(b => b.TripId == tripId && b.RiderId == userId);
            if (!onIt) return new BaseResponse<SafetyIncidentOutput>(default, ErrorCode.Forbidden);
            booking ??= bookingRepository.FirstOrDefault(b =>
                b.TripId == tripId && b.RiderId == userId && b.Status != BookingStatus.Cancelled);
        }

        var incident = new SafetyIncident
        {
            ReporterId = userId,
            Kind = input.Kind,
            TripId = trip?.Id,
            BookingId = booking?.Id,
            Location = input.Lat is { } lat && input.Lng is { } lng ? GeoFactory.Point(lat, lng) : null,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
            CreatedBy = userId,
        };
        incidentRepository.Create(incident);
        await unitOfWork.SaveAsync();

        var settings = (await appConfigurationService.Get()).Data;

        // The emergency contact hears about an SOS, with a link to follow the
        // ride when the reporter is a rider on one.
        if (input.Kind == SafetyIncidentKind.Sos && !string.IsNullOrWhiteSpace(user.EmergencyContactPhone))
        {
            var link = booking != null && booking.RiderId == userId
                ? await EnsureShareToken(booking)
                : null;
            var url = link == null ? null : BuildUrl(settings?.ShareBaseUrl, link);
            var where = incident.Location == null
                ? string.Empty
                : $" Location: https://maps.google.com/?q={incident.Location.Y:0.00000},{incident.Location.X:0.00000}";
            var follow = url == null ? string.Empty : $" Follow the trip: {url}";
            try
            {
                await smsSender.SendAsync(user.EmergencyContactPhone!,
                    $"Wanes SOS: {user.FirstName} pressed the emergency button.{where}{follow}");
                incident.EmergencyContactNotified = true;
                incidentRepository.Update(incident);
                await unitOfWork.SaveAsync();
            }
            catch
            {
                // Best-effort: the admin team still has the incident.
            }
        }

        await auditService.LogAsync(
            input.Kind == SafetyIncidentKind.Sos ? AuditActions.SafetySos : AuditActions.SafetyReport,
            nameof(SafetyIncident), incident.Id);

        var admins = await roleRepository.Where(r => r.Role == Roles.Admin)
            .Select(r => r.UserId).Distinct().ToListAsync();
        if (admins.Count > 0)
        {
            await notificationService.NotifyMany(admins, NotificationTemplate.SafetyIncidentAdmin,
                args: new
                {
                    kind = input.Kind == SafetyIncidentKind.Sos ? "SOS" : "report",
                    name = $"{user.FirstName} {user.LastName}".Trim(),
                },
                data: new { safetyIncidentId = incident.Id, tripId = incident.TripId });
        }

        incident.Reporter = user;
        return new BaseResponse<SafetyIncidentOutput>(new SafetyIncidentOutput(incident)
        {
            EmergencyNumber = settings?.EmergencyNumber ?? "911",
        });
    }

    public async Task<BaseResponse<ShareLinkOutput>> CreateShareLink(int bookingId)
    {
        var riderId = securityManager.RequireUserId();
        var booking = bookingRepository.FirstOrDefault(b => b.Id == bookingId && b.RiderId == riderId);
        if (booking == null) return new BaseResponse<ShareLinkOutput>(default, ErrorCode.BookingNotFound);
        if (booking.Status is BookingStatus.Cancelled or BookingStatus.NoShow)
            return new BaseResponse<ShareLinkOutput>(default, ErrorCode.Conflict);

        var token = await EnsureShareToken(booking);
        await auditService.LogAsync(AuditActions.ShareLinkCreate, nameof(Booking), booking.Id);
        var settings = (await appConfigurationService.Get()).Data;
        return new BaseResponse<ShareLinkOutput>(new ShareLinkOutput
        {
            Token = token,
            Url = BuildUrl(settings?.ShareBaseUrl, token),
        });
    }

    public async Task<BaseResponse> RevokeShareLink(int bookingId)
    {
        var riderId = securityManager.RequireUserId();
        var booking = bookingRepository.FirstOrDefault(b => b.Id == bookingId && b.RiderId == riderId);
        if (booking == null) return new BaseResponse(ErrorCode.BookingNotFound);

        booking.ShareToken = null;
        booking.ShareTokenCreatedAt = null;
        bookingRepository.Update(booking);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.ShareLinkRevoke, nameof(Booking), booking.Id);
        return new BaseResponse();
    }

    public async Task<BaseResponse<SharedTripOutput>> GetShared(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64)
            return new BaseResponse<SharedTripOutput>(default, ErrorCode.ShareLinkNotFound);

        var booking = bookingRepository.FirstOrDefault(b => b.ShareToken == token,
            q => q.Include(b => b.Rider)
                .Include(b => b.Trip).ThenInclude(t => t!.Driver)
                .Include(b => b.Trip).ThenInclude(t => t!.Vehicle));
        var trip = booking?.Trip;
        if (booking == null || trip == null)
            return new BaseResponse<SharedTripOutput>(default, ErrorCode.ShareLinkNotFound);

        var now = DateTime.UtcNow;
        var settled = !BookingStatusRules.IsLive(booking.Status);
        var settledAt = booking.ModificationDate ?? booking.CreationDate;
        if (settled && now - settledAt > LinkAfterRide)
            return new BaseResponse<SharedTripOutput>(default, ErrorCode.ShareLinkNotFound);

        var output = new SharedTripOutput
        {
            RiderFirstName = booking.Rider?.FirstName ?? string.Empty,
            OriginAddress = trip.OriginAddress,
            OriginLat = trip.Origin.Y,
            OriginLng = trip.Origin.X,
            DestinationAddress = trip.DestinationAddress,
            DestinationLat = trip.Destination.Y,
            DestinationLng = trip.Destination.X,
            DepartAt = trip.DepartAt,
            BookingStatus = booking.Status,
            TripStatus = trip.Status,
            DriverFirstName = trip.Driver?.FirstName,
            DriverRating = trip.Driver?.RatingAvg ?? 0,
            VehicleLabel = trip.Vehicle == null ? null : $"{trip.Vehicle.Make} {trip.Vehicle.Model}".Trim(),
            VehicleColor = trip.Vehicle?.Color,
            VehiclePlate = trip.Vehicle?.Plate,
            UpdatedAt = now,
        };

        // The car's position only while this person is actually on the ride.
        var underway = trip.Status is TripStatus.EnRoute or TripStatus.Arrived or TripStatus.Active;
        if (underway && !settled && trip.Driver?.LastLocation is { } point)
        {
            output.DriverLat = point.Y;
            output.DriverLng = point.X;
            output.DriverLocationAt = trip.Driver.LastLocationAt;
        }

        return new BaseResponse<SharedTripOutput>(output);
    }

    public async Task<BaseResponse<PageOutput<SafetyIncidentOutput>>> List(PageInput page, SafetyIncidentStatus? status)
    {
        IQueryable<SafetyIncident> query = incidentRepository.Query().Include(i => i.Reporter);
        if (status != null) query = query.Where(i => i.Status == status);
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim();
            query = query.Where(i => i.Reporter != null
                && (i.Reporter.FirstName.Contains(term) || i.Reporter.Phone.Contains(term)
                    || (i.Note != null && i.Note.Contains(term))));
        }

        var total = await query.CountAsync();
        // Open work first, SOS before reports, newest first.
        var rows = await query
            .OrderBy(i => i.Status)
            .ThenBy(i => i.Kind)
            .ThenByDescending(i => i.Id)
            .Paginate(page)
            .ToListAsync();
        return new BaseResponse<PageOutput<SafetyIncidentOutput>>(new PageOutput<SafetyIncidentOutput>
        {
            TotalRows = total,
            Data = rows.Select(i => new SafetyIncidentOutput(i)).ToList(),
        });
    }

    public async Task<BaseResponse<SafetyIncidentOutput>> Get(int id)
    {
        var incident = await incidentRepository.Query().Include(i => i.Reporter).FirstOrDefaultAsync(i => i.Id == id);
        return incident == null
            ? new BaseResponse<SafetyIncidentOutput>(default, ErrorCode.SafetyIncidentNotFound)
            : new BaseResponse<SafetyIncidentOutput>(new SafetyIncidentOutput(incident));
    }

    public async Task<BaseResponse<SafetyIncidentOutput>> Update(int id, UpdateSafetyIncidentInput input)
    {
        var adminId = securityManager.RequireUserId();
        var incident = await incidentRepository.Query().Include(i => i.Reporter).FirstOrDefaultAsync(i => i.Id == id);
        if (incident == null) return new BaseResponse<SafetyIncidentOutput>(default, ErrorCode.SafetyIncidentNotFound);

        incident.Status = input.Status;
        incident.AdminNote = string.IsNullOrWhiteSpace(input.AdminNote) ? incident.AdminNote : input.AdminNote.Trim();
        incident.HandledBy = adminId;
        incident.HandledAt = DateTime.UtcNow;
        incident.ModifiedBy = adminId;
        incident.ModificationDate = DateTime.UtcNow;
        incidentRepository.Update(incident);
        await unitOfWork.SaveAsync();
        await auditService.LogAsync(AuditActions.AdminSafetyUpdate, nameof(SafetyIncident), incident.Id);
        return new BaseResponse<SafetyIncidentOutput>(new SafetyIncidentOutput(incident));
    }

    private async Task<string> EnsureShareToken(Booking booking)
    {
        if (!string.IsNullOrEmpty(booking.ShareToken)) return booking.ShareToken;
        booking.ShareToken = BoardingCodes.NewShareToken();
        booking.ShareTokenCreatedAt = DateTime.UtcNow;
        bookingRepository.Update(booking);
        await unitOfWork.SaveAsync();
        return booking.ShareToken;
    }

    /// <summary>The CMS serves the public page at <c>/en/share/{token}</c>.</summary>
    private static string? BuildUrl(string? baseUrl, string token) =>
        string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl.TrimEnd('/')}/en/share/{token}";
}
