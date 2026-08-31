namespace Wanes.Shareds.Enums;

/// <summary>
/// Who an admin broadcast reaches. Disabled accounts are excluded from every
/// audience, and <see cref="Wanes.Areas.Domain.Users.User.NotifPush"/> still
/// decides whether a given recipient gets the *push* — the inbox row is written
/// either way.
/// </summary>
public enum NotificationAudience
{
    /// <summary>Every active account.</summary>
    All = 1,

    /// <summary>Accounts that can book a seat — the default for every signup.</summary>
    Riders = 2,

    /// <summary>Accounts that have applied to drive, at any verification stage.</summary>
    Drivers = 3,

    /// <summary>Drivers an admin has approved; the only ones who can take a hail.</summary>
    VerifiedDrivers = 4,
}
