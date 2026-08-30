using Wanes.Shareds.Attributes;

namespace Wanes.Shareds.Notifications.Fcm;

/// <summary>
/// Development FCM sender — logs instead of calling Firebase. Swap for a
/// FirebaseAdmin-backed implementation once a service-account key is configured.
/// </summary>
[SingletonInjectable]
public class FcmSender : IFcmSender
{
    private readonly ILogger<FcmSender> _logger;

    public FcmSender(ILogger<FcmSender> logger) => _logger = logger;

    public Task SendAsync(IEnumerable<string> deviceTokens, string title, string body,
        IDictionary<string, string>? data = null)
    {
        var tokens = deviceTokens.ToList();
        if (tokens.Count == 0) return Task.CompletedTask;
        _logger.LogInformation("FCM → {Count} device(s): {Title} — {Body}", tokens.Count, title, body);
        return Task.CompletedTask;
    }
}
