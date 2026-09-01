using Microsoft.Extensions.Options;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Users.Accounts;
using Wanes.Areas.Services.Users.Accounts.Models;
using Wanes.Shareds.Extensions;
using Wanes.Shareds.Models;
using Wanes.Shareds.Models.Config;
using Wanes.Shareds.SSE;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

/// <summary>
/// What signing out actually has to end.
///
/// Revoking the <c>UserLogin</c> row stops the API calls, and for a long time
/// that was all logout did — which left two things running that nobody could
/// see. A driver stayed <c>IsOnline</c>, so hail fan-out went on counting and
/// targeting a handset that had signed out; and the SSE stream stayed open,
/// because it authenticates once at connect and never again.
/// </summary>
public class LogoutTests
{
    private const string SessionKey = "session-a";

    private static AccountService Service(FakeUnitOfWork uow, SseConnectionManager sse,
        string sessionKey = SessionKey) => new(
        uow, new FakeSecurityManager(7) { SessionKey = sessionKey }, new FakeTokenGenerator(),
        new FakeSmsSender(), new FakeAuditService(), uow.Repository<OtpCode>(), uow.Repository<User>(),
        uow.Repository<UserRole>(), uow.Repository<UserLogin>(), sse,
        Options.Create(new OtpSettings { IsTesting = true, FixedCode = "1234", ExpiryMinutes = 5 }));

    /// <summary>An online driver signed in on one handset.</summary>
    private static FakeUnitOfWork SignedIn(params string[] sessionKeys)
    {
        var uow = new FakeUnitOfWork();
        var driver = Build.Driver(7);
        driver.IsOnline = true;
        driver.LastLocation = GeoFactory.Point(31.95, 35.92);
        uow.Store<User>().Add(driver);

        var id = 1;
        foreach (var key in sessionKeys.DefaultIfEmpty(SessionKey))
        {
            uow.Store<UserLogin>().Add(new UserLogin
            {
                Id = id++, UserId = 7, SessionKey = key,
                DeviceToken = "token-" + key,
                RefreshTokenHash = "hash-" + key,
                RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(30),
            });
        }
        return uow;
    }

    [Fact]
    public async Task Logging_out_revokes_the_session_row()
    {
        var uow = SignedIn();

        Assert.True((await Service(uow, new SseConnectionManager()).Logout()).Success);

        var login = uow.Store<UserLogin>().Single();
        Assert.True(login.IsDeleted);
        Assert.Null(login.RefreshTokenHash);
        Assert.Null(login.RefreshTokenExpiresAt);
        // The handset must stop being a push target, or the next account signed
        // in on it receives this one's notifications.
        Assert.Null(login.DeviceToken);
    }

    [Fact]
    public async Task Logging_out_takes_the_driver_offline()
    {
        var uow = SignedIn();

        await Service(uow, new SseConnectionManager()).Logout();

        Assert.False(uow.Store<User>().Single().IsOnline);
    }

    [Fact]
    public async Task Logging_out_of_one_handset_leaves_a_driver_signed_in_elsewhere_online()
    {
        // One driver, one car — but the app can be on a phone and a tablet, and
        // signing out of one must not knock them off mid-shift.
        var uow = SignedIn(SessionKey, "session-b");

        await Service(uow, new SseConnectionManager()).Logout();

        Assert.True(uow.Store<User>().Single().IsOnline);
        Assert.True(uow.Store<UserLogin>().Single(l => l.SessionKey == SessionKey).IsDeleted);
        Assert.False(uow.Store<UserLogin>().Single(l => l.SessionKey == "session-b").IsDeleted);
    }

    [Fact]
    public async Task Logging_out_ends_that_handsets_live_stream()
    {
        var uow = SignedIn();
        var sse = new SseConnectionManager();
        var stream = sse.Connect(7, SessionKey);

        await Service(uow, sse).Logout();

        // A completed channel is what ends the controller's read loop and closes
        // the response; while it stayed open a signed-out device kept receiving
        // the account's notifications.
        Assert.True(stream.Reader.Completion.IsCompleted);
    }

    [Fact]
    public async Task Logging_out_leaves_another_handsets_stream_alone()
    {
        var uow = SignedIn(SessionKey, "session-b");
        var sse = new SseConnectionManager();
        var mine = sse.Connect(7, SessionKey);
        var theirs = sse.Connect(7, "session-b");

        await Service(uow, sse).Logout();

        Assert.True(mine.Reader.Completion.IsCompleted);
        Assert.False(theirs.Reader.Completion.IsCompleted);
    }

    [Fact]
    public async Task A_revoked_stream_no_longer_receives_the_accounts_frames()
    {
        var uow = SignedIn(SessionKey, "session-b");
        var sse = new SseConnectionManager();
        var mine = sse.Connect(7, SessionKey);
        var theirs = sse.Connect(7, "session-b");

        await Service(uow, sse).Logout();
        await sse.SendAsync(7, "after-logout");
        await sse.BroadcastAsync("broadcast-after-logout");

        Assert.False(mine.Reader.TryRead(out _));
        Assert.True(theirs.Reader.TryRead(out var first));
        Assert.Equal("after-logout", first);
    }

    [Fact]
    public async Task Signing_out_twice_is_not_an_error()
    {
        // The app clears its session whatever the call answered, so a retry after
        // a flaky network reaches a row that is already gone.
        var uow = SignedIn();
        var sse = new SseConnectionManager();

        Assert.True((await Service(uow, sse).Logout()).Success);
        Assert.True((await Service(uow, sse).Logout()).Success);
    }

    [Fact]
    public async Task A_refresh_token_from_a_signed_out_session_is_refused()
    {
        // Signed in for real, so the token under test is one the server actually
        // issued — asserting that *some* string is refused would prove nothing.
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(Build.Driver(7));
        var sse = new SseConnectionManager();
        var signIn = Service(uow, sse);
        await signIn.RequestOtp(new RequestOtpInput { Phone = "+962790000002" });
        var auth = await signIn.VerifyOtp(new VerifyOtpInput { Phone = "+962790000002", Code = "1234" });
        var refreshToken = auth.Data!.RefreshToken;
        var sessionKey = uow.Store<UserLogin>().Single().SessionKey;

        await Service(uow, sse, sessionKey).Logout();
        var res = await Service(uow, sse, sessionKey)
            .Refresh(new RefreshTokenInput { RefreshToken = refreshToken });

        Assert.False(res.Success);
        Assert.Equal(ErrorCode.SessionExpired, res.ErrorCode);
    }
}
