using Wanes.Areas.Domain.Marketplace;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Configuration.Models;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Notifications.Models;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Shareds.Notifications.Fcm;
using Wanes.Shareds.Notifications.Sms;
using Wanes.Shareds.Security;
using Wanes.Shareds.Security.Token;

namespace Wanes.Tests.TestDoubles;

public class FakeSecurityManager : ISecurityManager
{
    public FakeSecurityManager(int? userId = 1) => UserId = userId;

    public int? UserId { get; set; }
    public int RequireUserId() => UserId ?? throw new AppException(ErrorCode.Unauthorized);
    public IReadOnlyCollection<Roles> Roles { get; set; } = [Wanes.Shareds.Enums.Roles.User];
    public bool IsInRole(Roles role) => Roles.Contains(role);
    public string? SessionKey { get; set; } = "test-session";
}

public class FakeAuditService : IAuditService
{
    public List<string> Actions { get; } = [];

    public Task LogAsync(string action, string? entityType = null, int? entityId = null,
        object? before = null, object? after = null)
    {
        Actions.Add(action);
        return Task.CompletedTask;
    }
}

public class FakeNotificationService : INotificationService
{
    /// <summary>"userId:template" per delivery, in order, so a test can assert who was told what.</summary>
    public List<string> Sent { get; } = [];
    public int NearbyDriverCount { get; set; } = 2;

    public Task Notify(int userId, NotificationTemplate template, object? args = null, object? data = null)
    {
        Sent.Add($"{userId}:{template}");
        return Task.CompletedTask;
    }

    public Task NotifyRaw(int userId, NotificationType type, LocalizedText text, string? dataJson)
    {
        Sent.Add($"{userId}:{type}");
        return Task.CompletedTask;
    }

    public Task NotifyMany(IEnumerable<int> userIds, NotificationTemplate template, object? args = null,
        object? data = null)
    {
        foreach (var userId in userIds.Distinct()) Sent.Add($"{userId}:{template}");
        return Task.CompletedTask;
    }

    public Task<int> NotifyAudience(NotificationAudience audience, NotificationType type, LocalizedText text,
        object? data = null)
    {
        Sent.Add($"{audience}:{type}");
        return Task.FromResult(0);
    }

    public Task<int> NotifyUsersRaw(IEnumerable<int> userIds, NotificationType type, LocalizedText text,
        string? dataJson)
    {
        var ids = userIds.Distinct().ToList();
        foreach (var userId in ids) Sent.Add($"{userId}:{type}");
        return Task.FromResult(ids.Count);
    }

    public Task<int> NotifyNearbyDrivers(RideRequest request)
    {
        Sent.Add($"nearby:{request.Id}");
        return Task.FromResult(NearbyDriverCount);
    }

    /// <summary>
    /// Records that the reverse match was asked for, and for which trip. Which
    /// riders it would actually reach is the real service's business — see
    /// <c>ReverseMatchTests</c> — so the fake only pins whether the caller asked.
    /// </summary>
    /// <remarks>
    /// Still takes a Trip: this one is about a trip that exists, offering its
    /// spare seats to riders who are still waiting on demand of their own.
    /// </summary>
    public Task NotifyWaitingRiders(Trip trip, User driver)
    {
        Sent.Add($"waiting:{trip.Id}");
        return Task.CompletedTask;
    }

    public Task NotifyRideRequestClosed(int rideRequestId, RiderTripClosedReason reason)
    {
        Sent.Add($"request-{rideRequestId}:{reason}");
        return Task.CompletedTask;
    }

    public Task<BaseResponse<NotificationFeed>> GetUserNotifications() =>
        Task.FromResult(new BaseResponse<NotificationFeed>(new NotificationFeed()));

    public Task<BaseResponse> MarkRead(int id) => Task.FromResult(new BaseResponse());
    public Task<BaseResponse> MarkAllRead() => Task.FromResult(new BaseResponse());
    public Task<BaseResponse> Delete(int id) => Task.FromResult(new BaseResponse());
    public Task<BaseResponse> RegisterDevice(RegisterDeviceInput input) => Task.FromResult(new BaseResponse());
    public Task<BaseResponse> ClearDevice() => Task.FromResult(new BaseResponse());
}

/// <summary>
/// The shipped defaults, with every window a test might want to move.
///
/// Deliberately not a mock: these values are read on nearly every path, and a
/// test that had to arrange four of them before it could book a seat would be
/// testing the arrangement.
/// </summary>
public class FakeAppConfigurationService : IAppConfigurationService
{
    public int ConfirmCutoffMinutes { get; set; } = TripConfirmationRules.DefaultCutoffMinutes;

    public int ConfirmDecisionLeadMinutes { get; set; } =
        TripConfirmationRules.DefaultDecisionLeadMinutes;

    /// <summary>
    /// The marketplace's seed for a trip whose driver named no threshold —
    /// production's own default, not 1. A suite that quietly seeded "no
    /// condition" would never exercise the rule that ships.
    /// </summary>
    public int MinimumPassengersDefault { get; set; } =
        TripConfirmationRules.DefaultMinimumPassengers;

    /// <summary>
    /// Zero: first interest wins, which is what ships. A test about competing
    /// offers sets it and gets the other marketplace, with no other change.
    /// </summary>
    public int DriverSelectionWindowMinutes { get; set; } =
        DriverSelectionRules.ImmediateSelection;

    public double AverageSpeedKmh { get; set; } = RiderTripRules.DefaultAverageSpeedKmh;

    // The rates a claimed trip is priced from when the driver named no figure.
    // Default to the shipped ones so a test that does not care about price gets
    // the real arithmetic.
    public decimal FareBaseAmount { get; set; } = FareRules.DefaultBaseAmount;
    public decimal FarePerKm { get; set; } = FareRules.DefaultPerKm;

    /// <summary>
    /// Zero, where production collects offers for twenty minutes. The suite's
    /// requests leave in two hours, so the shipped value would turn every
    /// formation test into a test of the window; the window tests set it.
    /// </summary>
    public int ScheduledSelectionWindowMinutes { get; set; } = DriverSelectionRules.ImmediateSelection;

    public bool RiderOfferChoice { get; set; } = true;

    /// <summary>On, as shipped: offers in the suite say they accept a shared trip.</summary>
    public bool RequireSharedTermsAcceptance { get; set; } = true;

    public int FreeCancelGraceMinutes { get; set; } = ReliabilityRules.DefaultFreeCancelGraceMinutes;
    public int LateCancelLeadMinutes { get; set; } = ReliabilityRules.DefaultLateCancelLeadMinutes;
    public int ReliabilityWarnPoints { get; set; } = ReliabilityRules.DefaultWarnPoints;
    public int ReliabilitySuspendPoints { get; set; } = ReliabilityRules.DefaultSuspendPoints;
    public int ReliabilityWindowDays { get; set; } = ReliabilityRules.DefaultWindowDays;
    public int SuspensionDays { get; set; } = ReliabilityRules.DefaultSuspensionDays;

    /// <summary>
    /// Off, where production has it on: the tracking tests are about the seat
    /// ladder, not the code. The boarding-code tests turn it on.
    /// </summary>
    public bool BoardingCodeRequired { get; set; }

    public string EmergencyNumber { get; set; } = "911";
    public string? ShareBaseUrl { get; set; }

    public Task<BaseResponse<AppConfigurationOutput>> Get() =>
        Task.FromResult(new BaseResponse<AppConfigurationOutput>(Output()));

    public Task<BaseResponse<AppConfigurationOutput>> Update(AppConfigurationInput input) =>
        Task.FromResult(new BaseResponse<AppConfigurationOutput>(new AppConfigurationOutput
        {
            ConfirmCutoffMinutes = input.ConfirmCutoffMinutes,
            ConfirmDecisionLeadMinutes = input.ConfirmDecisionLeadMinutes,
            MinimumPassengersDefault = input.MinimumPassengersDefault,
            DriverSelectionWindowMinutes = input.DriverSelectionWindowMinutes,
            AverageSpeedKmh = input.AverageSpeedKmh,
            FareBaseAmount = input.FareBaseAmount,
            FarePerKm = input.FarePerKm,
        }));

    private AppConfigurationOutput Output() => new()
    {
        ConfirmCutoffMinutes = ConfirmCutoffMinutes,
        ConfirmDecisionLeadMinutes = ConfirmDecisionLeadMinutes,
        MinimumPassengersDefault = MinimumPassengersDefault,
        DriverSelectionWindowMinutes = DriverSelectionWindowMinutes,
        AverageSpeedKmh = AverageSpeedKmh,
        FareBaseAmount = FareBaseAmount,
        FarePerKm = FarePerKm,
        ScheduledSelectionWindowMinutes = ScheduledSelectionWindowMinutes,
        RiderOfferChoice = RiderOfferChoice,
        RequireSharedTermsAcceptance = RequireSharedTermsAcceptance,
        FreeCancelGraceMinutes = FreeCancelGraceMinutes,
        LateCancelLeadMinutes = LateCancelLeadMinutes,
        ReliabilityWarnPoints = ReliabilityWarnPoints,
        ReliabilitySuspendPoints = ReliabilitySuspendPoints,
        ReliabilityWindowDays = ReliabilityWindowDays,
        SuspensionDays = SuspensionDays,
        BoardingCodeRequired = BoardingCodeRequired,
        EmergencyNumber = EmergencyNumber,
        ShareBaseUrl = ShareBaseUrl,
    };
}

public class FakeSmsSender : ISmsSender
{
    public List<string> Sent { get; } = [];

    public Task SendAsync(string phone, string message)
    {
        Sent.Add($"{phone}:{message}");
        return Task.CompletedTask;
    }
}

public class FakeTokenGenerator : ITokenGenerator
{
    public (string Token, DateTime ExpiresAt) Generate(int userId, IEnumerable<Roles> roles, string sessionKey) =>
        ($"token-{userId}-{sessionKey}", DateTime.UtcNow.AddMinutes(15));

    // Deterministic and reversible-by-eye: the hash is the raw value with a marker,
    // so a test can assert the stored hash matches the token it handed out.
    public (string Token, string Hash, DateTime ExpiresAt) GenerateRefreshToken()
    {
        var token = $"refresh-{Guid.NewGuid():N}";
        return (token, HashRefreshToken(token), DateTime.UtcNow.AddDays(30));
    }

    public string HashRefreshToken(string token) => $"hash:{token}";
}

/// <summary>Push turned off, the way a machine with no Firebase credentials runs.</summary>
public class FakeFcmSender : IFcmSender
{
    public bool IsConfigured => false;

    public Task<FcmSendResult> SendAsync(IEnumerable<string> deviceTokens, string title, string body,
        IDictionary<string, string>? data = null) =>
        Task.FromResult(new FcmSendResult());
}

/// <summary>
/// File storage in a dictionary. Keys are handed out the same shape the real
/// one uses, so a test can assert a document was written under the right folder
/// without touching a disk.
/// </summary>
public class FakeFileStorage : Wanes.Shareds.Files.IFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public List<string> Deleted { get; } = [];

    public async Task<string> SaveAsync(string folder, string extension, Stream content, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var key = $"{folder}/{Guid.NewGuid():N}{extension}";
        Files[key] = buffer.ToArray();
        return key;
    }

    public Stream? OpenRead(string key) =>
        Files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null;

    public Task DeleteAsync(string key)
    {
        Deleted.Add(key);
        Files.Remove(key);
        return Task.CompletedTask;
    }
}
