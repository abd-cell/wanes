using Wanes.Shareds.Attributes;

namespace Wanes.Shareds.Notifications.Sms;

/// <summary>Development SMS sender — logs instead of calling a provider (e.g. Twilio).</summary>
[SingletonInjectable]
public class SmsSender : ISmsSender
{
    private readonly ILogger<SmsSender> _logger;

    public SmsSender(ILogger<SmsSender> logger) => _logger = logger;

    public Task SendAsync(string phone, string message)
    {
        _logger.LogInformation("SMS → {Phone}: {Message}", phone, message);
        return Task.CompletedTask;
    }
}
