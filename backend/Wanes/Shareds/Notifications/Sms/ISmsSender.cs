namespace Wanes.Shareds.Notifications.Sms;

public interface ISmsSender
{
    Task SendAsync(string phone, string message);
}
