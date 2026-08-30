namespace Wanes.Shareds.Notifications.Fcm;

public interface IFcmSender
{
    Task SendAsync(IEnumerable<string> deviceTokens, string title, string body,
        IDictionary<string, string>? data = null);
}
