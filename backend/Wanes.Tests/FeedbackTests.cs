using Wanes.Areas.Domain.Bookings;
using Wanes.Areas.Domain.Support;
using Wanes.Areas.Domain.Trips;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Domain.Vehicles;
using Wanes.Areas.Services.Management;
using Wanes.Areas.Services.Management.Models;
using Wanes.Areas.Services.Support;
using Wanes.Areas.Services.Support.Models;
using Wanes.Shareds.Enums;
using Wanes.Shareds.Models;
using Wanes.Shareds.Notifications;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// Complaints and suggestions: who may file one, what the desk may change, and
/// when the user hears back.
///
/// The rules worth pinning are the ones the shape of the CRUD does not already
/// give away — a trip reference has to be the caller's own, the limit is on
/// what is still open rather than on the clock, a blank reply means "nothing to
/// say yet" and not "erase what I wrote", and only a real answer is worth a
/// notification.
/// </summary>
public class FeedbackTests
{
    private const int RiderId = 5;
    private const int OtherRiderId = 6;
    private const int DriverId = 2;
    private const int AdminId = 1;
    private const int TripId = 30;

    private static FeedbackService Feedback(FakeUnitOfWork uow, int actingUserId,
        FakeAuditService? audit = null) =>
        new(uow, new FakeSecurityManager(actingUserId), audit ?? new FakeAuditService(),
            uow.Repository<Feedback>(), uow.Repository<Trip>(), uow.Repository<Booking>(),
            uow.Repository<User>());

    private static AdminFeedbackService Admin(FakeUnitOfWork uow, FakeNotificationService notifications,
        FakeAuditService? audit = null) =>
        new(uow, new FakeSecurityManager(AdminId), audit ?? new FakeAuditService(), notifications,
            uow.Repository<Feedback>(), uow.Repository<User>());

    /// <summary>An Arabic-speaking rider with a completed booking on one driver's trip, plus an unrelated rider.</summary>
    private static FakeUnitOfWork Scene()
    {
        var uow = new FakeUnitOfWork();

        var rider = Build.Rider(RiderId);
        rider.Language = Language.Ar;
        uow.Store<User>().Add(rider);
        uow.Store<User>().Add(Build.Rider(OtherRiderId));
        uow.Store<User>().Add(Build.Driver(DriverId));

        uow.Store<Vehicle>().Add(Build.Vehicle(10, DriverId));
        uow.Store<Trip>().Add(Build.Trip(TripId, DriverId, 10));
        uow.Store<Booking>().Add(new Booking
        {
            Id = 40, TripId = TripId, RiderId = RiderId, Seats = 1, Status = BookingStatus.Completed,
        });

        return uow;
    }

    private static FeedbackInput Complaint(int? tripId = null) => new()
    {
        Kind = FeedbackKind.Complaint,
        TripId = tripId,
        Subject = "Driver took a long detour",
        Message = "We went the wrong way for twenty minutes and I was late.",
    };

    [Fact]
    public async Task Submit_stores_the_users_language_so_the_desk_answers_in_it()
    {
        var uow = Scene();

        var response = await Feedback(uow, RiderId).Submit(Complaint());

        Assert.True(response.Success);
        var stored = Assert.Single(uow.Store<Feedback>());
        Assert.Equal(Language.Ar, stored.Language);
        Assert.Equal(FeedbackStatus.New, stored.Status);
        Assert.Equal(RiderId, stored.UserId);
    }

    [Fact]
    public async Task Submit_is_audited()
    {
        var uow = Scene();
        var audit = new FakeAuditService();

        await Feedback(uow, RiderId, audit).Submit(Complaint());

        Assert.Contains("feedback.submit", audit.Actions);
    }

    [Fact]
    public async Task Submit_accepts_a_trip_the_rider_was_on()
    {
        var uow = Scene();

        var response = await Feedback(uow, RiderId).Submit(Complaint(TripId));

        Assert.True(response.Success);
        Assert.Equal(TripId, response.Data!.TripId);
    }

    [Fact]
    public async Task Submit_accepts_a_trip_the_driver_drove()
    {
        var uow = Scene();

        var response = await Feedback(uow, DriverId).Submit(Complaint(TripId));

        Assert.True(response.Success);
        Assert.Equal(TripId, response.Data!.TripId);
    }

    /// <summary>
    /// A stranger's trip id would hang the complaint off someone else's ride,
    /// and would answer "does trip 30 exist?" for anyone who cared to ask.
    /// </summary>
    [Fact]
    public async Task Submit_refuses_a_trip_the_user_was_never_on()
    {
        var uow = Scene();

        var response = await Feedback(uow, OtherRiderId).Submit(Complaint(TripId));

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.TripNotFound, response.ErrorCode);
        Assert.Empty(uow.Store<Feedback>());
    }

    [Fact]
    public async Task Submit_stops_at_the_open_submission_cap()
    {
        var uow = Scene();
        var service = Feedback(uow, RiderId);

        for (var i = 0; i < FeedbackRules.MaxOpenPerUser; i++)
            Assert.True((await service.Submit(Complaint())).Success);

        var response = await service.Submit(Complaint());

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.TooManyOpenFeedback, response.ErrorCode);
        Assert.Equal(FeedbackRules.MaxOpenPerUser, uow.Store<Feedback>().Count);
    }

    /// <summary>
    /// The cap counts what is still waiting, not what was ever sent — that is
    /// the whole reason it is a queue depth and not a rate. A desk that answers
    /// hands the user their slot back.
    /// </summary>
    [Fact]
    public async Task Closing_a_submission_frees_a_slot()
    {
        var uow = Scene();
        var service = Feedback(uow, RiderId);
        for (var i = 0; i < FeedbackRules.MaxOpenPerUser; i++) await service.Submit(Complaint());

        uow.Store<Feedback>()[0].Status = FeedbackStatus.Resolved;

        Assert.True((await service.Submit(Complaint())).Success);
    }

    [Fact]
    public async Task Mine_returns_only_the_callers_own_submissions_newest_first()
    {
        var uow = Scene();
        await Feedback(uow, RiderId).Submit(Complaint());
        await Feedback(uow, OtherRiderId).Submit(Complaint());
        await Feedback(uow, RiderId).Submit(Complaint());

        var response = await Feedback(uow, RiderId).Mine();

        Assert.True(response.Success);
        Assert.Equal(2, response.Data!.Count);
        Assert.Equal(new[] { 3, 1 }, response.Data.Select(f => f.Id).ToArray());
    }

    [Fact]
    public async Task Replying_notifies_the_user_and_records_who_answered()
    {
        var uow = Scene();
        await Feedback(uow, RiderId).Submit(Complaint());
        var notifications = new FakeNotificationService();

        var response = await Admin(uow, notifications).Update(1, new FeedbackReviewInput
        {
            Status = FeedbackStatus.Resolved,
            Reply = "Sorry about that. We have spoken to the driver.",
        });

        Assert.True(response.Success);
        Assert.Equal(FeedbackStatus.Resolved, response.Data!.Status);
        Assert.Equal(AdminId, uow.Store<Feedback>()[0].RepliedBy);
        Assert.NotNull(uow.Store<Feedback>()[0].RepliedAt);
        Assert.Contains($"{RiderId}:{NotificationTemplate.FeedbackReplied}", notifications.Sent);
    }

    /// <summary>
    /// Picking a row up is desk bookkeeping. Notifying on it would train the
    /// user to ignore the one notification that carries an actual answer.
    /// </summary>
    [Fact]
    public async Task Taking_a_submission_into_review_notifies_nobody()
    {
        var uow = Scene();
        await Feedback(uow, RiderId).Submit(Complaint());
        var notifications = new FakeNotificationService();

        var response = await Admin(uow, notifications)
            .Update(1, new FeedbackReviewInput { Status = FeedbackStatus.InReview });

        Assert.True(response.Success);
        Assert.Equal(FeedbackStatus.InReview, uow.Store<Feedback>()[0].Status);
        Assert.Null(uow.Store<Feedback>()[0].Reply);
        Assert.Empty(notifications.Sent);
    }

    /// <summary>
    /// The status dropdown and the reply box submit together, so closing a
    /// thread whose answer is already written must neither blank the answer nor
    /// notify the user a second time about the same words.
    /// </summary>
    [Fact]
    public async Task Closing_an_already_answered_submission_keeps_the_reply_and_stays_quiet()
    {
        var uow = Scene();
        await Feedback(uow, RiderId).Submit(Complaint());
        var notifications = new FakeNotificationService();
        var admin = Admin(uow, notifications);
        await admin.Update(1, new FeedbackReviewInput
        {
            Status = FeedbackStatus.InReview,
            Reply = "Looking into it.",
        });
        notifications.Sent.Clear();

        await admin.Update(1, new FeedbackReviewInput { Status = FeedbackStatus.Resolved, Reply = "   " });

        Assert.Equal("Looking into it.", uow.Store<Feedback>()[0].Reply);
        Assert.Equal(FeedbackStatus.Resolved, uow.Store<Feedback>()[0].Status);
        Assert.Empty(notifications.Sent);
    }

    /// <summary>The user reads the desk's answer back through their own endpoint.</summary>
    [Fact]
    public async Task The_reply_comes_back_on_the_users_own_list()
    {
        var uow = Scene();
        await Feedback(uow, RiderId).Submit(Complaint());
        await Admin(uow, new FakeNotificationService()).Update(1, new FeedbackReviewInput
        {
            Status = FeedbackStatus.Resolved,
            Reply = "Fixed.",
        });

        var mine = await Feedback(uow, RiderId).Mine();

        Assert.Equal("Fixed.", mine.Data![0].Reply);
        Assert.Equal(FeedbackStatus.Resolved, mine.Data[0].Status);
    }

    [Fact]
    public async Task Admin_delete_is_soft_and_hides_the_row_from_the_user()
    {
        var uow = Scene();
        await Feedback(uow, RiderId).Submit(Complaint());

        var response = await Admin(uow, new FakeNotificationService()).Delete(1);

        Assert.True(response.Success);
        Assert.True(uow.Store<Feedback>()[0].IsDeleted);
        Assert.Empty((await Feedback(uow, RiderId).Mine()).Data!);
    }

    [Fact]
    public async Task Admin_reads_a_missing_submission_as_FeedbackNotFound()
    {
        var uow = Scene();

        var response = await Admin(uow, new FakeNotificationService()).Get(99);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.FeedbackNotFound, response.ErrorCode);
    }

    [Fact]
    public async Task Admin_list_filters_by_kind_and_names_the_author()
    {
        var uow = Scene();
        await Feedback(uow, RiderId).Submit(Complaint());
        await Feedback(uow, RiderId).Submit(new FeedbackInput
        {
            Kind = FeedbackKind.Suggestion,
            Subject = "Let me save a favourite driver",
            Message = "I would like to ride with the same driver again.",
        });

        var response = await Admin(uow, new FakeNotificationService())
            .List(new PageInput(), FeedbackKind.Suggestion, null);

        Assert.True(response.Success);
        var row = Assert.Single(response.Data!.Data);
        Assert.Equal(FeedbackKind.Suggestion, row.Kind);
        Assert.False(string.IsNullOrEmpty(row.UserName));
    }
}
