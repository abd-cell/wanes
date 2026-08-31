namespace Wanes.Shareds.Notifications.Fcm;

/// <summary>
/// Outcome of one multicast send. <see cref="InvalidTokens"/> carries the device
/// tokens Firebase rejected as unregistered or malformed — callers should delete
/// those so a dead device isn't retried on every future notification.
/// </summary>
public sealed class FcmSendResult
{
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public IReadOnlyList<string> InvalidTokens { get; init; } = [];

    /// <summary>Nothing was sent (no tokens, or push is disabled).</summary>
    public static readonly FcmSendResult None = new();
}
