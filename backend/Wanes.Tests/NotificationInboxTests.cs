using Microsoft.Extensions.Logging.Abstractions;
using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Notifications;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Notifications;
using Wanes.Areas.Services.Users.Availability;
using Wanes.Shareds.Constants;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.SSE;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// Clearing a notification off the inbox.
///
/// It is a soft delete, and the whole point of the feature is the asymmetry:
/// the row outlives the delete, hidden from the owner by the repository's
/// default filter but deliberately still listed by the console, which passes
/// includeDeleted — a notification a user cleared is only visible there.
/// </summary>
public class NotificationInboxTests
{
    private const int RiderId = 5;
    private const int OtherRiderId = 6;
    private const int AdminId = 1;

    private static NotificationService Inbox(FakeUnitOfWork uow, int actingUserId, FakeAuditService audit) =>
        new(uow, audit, new FakeSecurityManager(actingUserId), new FakeFcmSender(),
            new SseConnectionManager(), NullLogger<NotificationService>.Instance,
            uow.Repository<UserNotification>(), uow.Repository<UserLogin>(), uow.Repository<User>(),
            uow.Repository<Trip>(), uow.Repository<Booking>(),
            uow.Repository<RideRequest>(), uow.Repository<RideRequestParticipant>(),
            new DriverAvailabilityService(uow.Repository<Trip>(), new FakeSecurityManager()));

    private static AdminNotificationService Console(FakeUnitOfWork uow) =>
        new(uow, new FakeAuditService(), new FakeNotificationService(),
            uow.Repository<UserNotification>(), uow.Repository<User>());

    /// <summary>Two unread notifications for our rider, one for somebody else.</summary>
    private static FakeUnitOfWork Scene()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Rider(RiderId));
        uow.Store<User>().Add(Build.Rider(OtherRiderId));

        uow.Store<UserNotification>().Add(Note(1, RiderId, "First"));
        uow.Store<UserNotification>().Add(Note(2, RiderId, "Second"));
        uow.Store<UserNotification>().Add(Note(3, OtherRiderId, "Theirs"));
        return uow;
    }

    private static UserNotification Note(int id, int userId, string title) => new()
    {
        Id = id, UserId = userId, Type = NotificationType.General, Title = title, Body = title,
    };

    [Fact]
    public async Task Delete_hides_the_row_from_the_feed_but_keeps_it_in_the_table()
    {
        var uow = Scene();
        var audit = new FakeAuditService();

        var deleted = await Inbox(uow, RiderId, audit).Delete(1);
        Assert.True(deleted.Success);

        var feed = await Inbox(uow, RiderId, audit).GetUserNotifications();
        Assert.Equal([2], feed.Data!.Items.Select(i => i.Id));

        // The badge has to agree with the list: a cleared unread row must stop
        // being counted, or the inbox shows a count it cannot account for.
        Assert.Equal(1, feed.Data!.UnreadCount);

        var row = uow.Store<UserNotification>().Single(n => n.Id == 1);
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletionDate);

        // Audited, because the row survives: this is what tells the console it
        // was the owner who cleared it and not an admin.
        Assert.Contains(AuditActions.NotificationDelete, audit.Actions);
    }

    [Fact]
    public async Task Delete_refuses_somebody_elses_notification()
    {
        var uow = Scene();

        var response = await Inbox(uow, RiderId, new FakeAuditService()).Delete(3);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.NotFound, response.ErrorCode);
        Assert.False(uow.Store<UserNotification>().Single(n => n.Id == 3).IsDeleted);
    }

    [Fact]
    public async Task A_cleared_notification_can_no_longer_be_marked_read()
    {
        var uow = Scene();
        var audit = new FakeAuditService();
        await Inbox(uow, RiderId, audit).Delete(1);

        var single = await Inbox(uow, RiderId, audit).MarkRead(1);
        Assert.Equal(ErrorCode.NotFound, single.ErrorCode);

        // Nor does "mark all read" sweep it up on the way past — it is gone from
        // the user's world, so it stays as it was.
        await Inbox(uow, RiderId, audit).MarkAllRead();
        Assert.False(uow.Store<UserNotification>().Single(n => n.Id == 1).IsRead);
        Assert.True(uow.Store<UserNotification>().Single(n => n.Id == 2).IsRead);
    }

    [Fact]
    public async Task The_console_still_lists_what_the_user_cleared()
    {
        var uow = Scene();
        await Inbox(uow, RiderId, new FakeAuditService()).Delete(1);

        var page = new PageInput { PageNumber = 1, PageSize = 25 };

        var all = await Console(uow).List(page, null, null, null, null);
        Assert.Equal(3, all.Data!.TotalRows);
        Assert.True(all.Data!.Data.Single(r => r.Id == 1).IsDeleted);

        // …and can narrow to just those, which is the only way to tell at a
        // glance what users are throwing away.
        var cleared = await Console(uow).List(page, null, null, null, true);
        Assert.Equal([1], cleared.Data!.Data.Select(r => r.Id));

        var stats = await Console(uow).Stats();
        Assert.Equal(3, stats.Data!.Total);
        Assert.Equal(1, stats.Data!.Deleted);
    }
}
