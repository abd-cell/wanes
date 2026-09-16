using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Notifications.Models;
using Wanes.Shareds.Attributes;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;

namespace Wanes.Areas.Services.Notifications;

[TransientInjectable]
public interface INotificationService
{
    /// <summary>
    /// Stores a notification and fans it out over FCM (push) and SSE (in-app).
    /// Never throws — delivery is best-effort and must not fail its caller.
    ///
    /// Takes a template plus arguments rather than finished strings so the
    /// message can be rendered in both languages; <paramref name="args"/> is any
    /// object whose properties name the template's placeholders.
    /// </summary>
    Task Notify(int userId, NotificationTemplate template, object? args = null, object? data = null);

    /// <summary>
    /// <see cref="Notify"/> with wording supplied directly and an already-serialized
    /// payload — used by the admin console, which cannot be templated because the
    /// text is typed by hand.
    /// </summary>
    Task NotifyRaw(int userId, NotificationType type, LocalizedText text, string? dataJson);

    /// <summary>Same as <see cref="Notify"/> for a set of recipients, skipping duplicates.</summary>
    Task NotifyMany(IEnumerable<int> userIds, NotificationTemplate template, object? args = null,
        object? data = null);

    /// <summary>
    /// Fans one notification out to a whole audience — the admin broadcast path.
    /// Returns the number of recipients an inbox row was written for.
    ///
    /// Unlike <see cref="NotifyMany"/> this pushes with a single multicast for
    /// the entire audience rather than one send per recipient, so the cost does
    /// not scale with the user count.
    /// </summary>
    Task<int> NotifyAudience(NotificationAudience audience, NotificationType type, LocalizedText text,
        object? data = null);

    /// <summary>
    /// <see cref="NotifyAudience"/> for a hand-picked set of recipients rather
    /// than a whole audience — the admin console's targeted send, whose wording
    /// is typed by hand and so cannot be templated. Disabled accounts and
    /// duplicate ids are dropped. Returns the number of inbox rows written.
    /// </summary>
    Task<int> NotifyUsersRaw(IEnumerable<int> userIds, NotificationType type, LocalizedText text,
        string? dataJson);

    /// <summary>
    /// Notifies verified, online drivers near an open ride request who are free
    /// to serve it and satisfy its conditions — a driver mid-trip, already
    /// promised to a departure this close, on the request themselves, or
    /// excluded by the riders' own requirements is skipped. Returns the count
    /// reached.
    /// </summary>
    Task<int> NotifyNearbyDrivers(RideRequest request);

    /// <summary>
    /// Tells every connected client that a trip is no longer waiting for a
    /// driver, so a driver holding its card drops it instead of tapping Take on
    /// something that cannot be taken.
    ///
    /// Deliberately not a notification: it writes no inbox row and raises no
    /// push. A driver does not need to be told, out of app and by name, that a
    /// trip they never answered went away — they need the card gone the moment
    /// it does. <paramref name="reason"/> is what became of it, which is what the
    /// driver's screen words its one-line notice from — see
    /// <see cref="RiderTripClosedReason"/> for why it is not the trip's status.
    /// </summary>
    Task NotifyRideRequestClosed(int rideRequestId, RiderTripClosedReason reason);

    /// <summary>
    /// The reverse of <see cref="NotifyNearbyDrivers"/>: tells riders sitting on
    /// an open request that a trip they could take now exists. Best-effort, and
    /// called after the trip is committed.
    ///
    /// Both routes into a new trip use it — a driver publishing one, and a trip
    /// formed from demand. The second matters as much as the first: that trip
    /// carries the seats the selected driver did not sell to the pool, and
    /// without this nobody else ever learns it is there.
    /// </summary>
    Task NotifyWaitingRiders(Trip trip, User driver);

    Task<BaseResponse<NotificationFeed>> GetUserNotifications();
    Task<BaseResponse> MarkRead(int id);
    Task<BaseResponse> MarkAllRead();

    /// <summary>
    /// Clears one notification off the caller's own inbox. Soft delete: the row
    /// stays in the table and the admin console still lists it, it just stops
    /// being served to — or counted for — the user who owns it.
    /// </summary>
    Task<BaseResponse> Delete(int id);

    /// <summary>Attaches an FCM token to the calling device session.</summary>
    Task<BaseResponse> RegisterDevice(RegisterDeviceInput input);

    /// <summary>Detaches the FCM token from the calling device session (sign-out, push opt-out).</summary>
    Task<BaseResponse> ClearDevice();
}
