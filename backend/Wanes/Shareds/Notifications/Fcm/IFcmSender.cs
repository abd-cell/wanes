namespace Wanes.Shareds.Notifications.Fcm;

public interface IFcmSender
{
    /// <summary>True when a Firebase service account was loaded and push is enabled.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Delivers one notification to every supplied device token. Never throws —
    /// a push failure must not fail the business operation that triggered it.
    /// </summary>
    Task<FcmSendResult> SendAsync(IEnumerable<string> deviceTokens, string title, string body,
        IDictionary<string, string>? data = null);
}
