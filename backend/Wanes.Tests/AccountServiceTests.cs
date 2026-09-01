using Microsoft.Extensions.Options;
using Wanes.Areas.Domain.Users;
using Wanes.Areas.Services.Users.Accounts;
using Wanes.Areas.Services.Users.Accounts.Models;
using Wanes.Shareds.Models.Config;
using Wanes.Shareds.SSE;
using Wanes.Tests.TestDoubles;

namespace Wanes.Tests;

public class AccountServiceTests
{
    private static AccountService Service(FakeUnitOfWork uow) => new(
        uow, new FakeSecurityManager(1), new FakeTokenGenerator(), new FakeSmsSender(),
        new FakeAuditService(), uow.Repository<OtpCode>(), uow.Repository<User>(),
        uow.Repository<UserRole>(), uow.Repository<UserLogin>(), new SseConnectionManager(),
        Options.Create(new OtpSettings { IsTesting = true, FixedCode = "1234", ExpiryMinutes = 5 }));

    [Theory]
    [InlineData("0790000000")]      // local, trunk prefix
    [InlineData("00962790000000")]  // international, 00 prefix
    [InlineData("+962 79 000 0000")] // spaced E.164
    [InlineData("790000000")]       // bare subscriber number
    public async Task Sign_in_resolves_the_same_account_whatever_the_number_format(string typed)
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(new User
        {
            Id = 7, Phone = "+962790000000", PhoneKey = "790000000", PhoneVerified = true,
        });
        var svc = Service(uow);

        Assert.True((await svc.RequestOtp(new RequestOtpInput { Phone = typed })).Success);
        var res = await svc.VerifyOtp(new VerifyOtpInput { Phone = typed, Code = "1234" });

        Assert.True(res.Success);
        Assert.False(res.Data!.IsNewUser);
        Assert.Equal(7, res.Data.Profile.Id);
        Assert.Single(uow.Store<User>());
    }

    [Fact]
    public async Task Otp_requested_in_one_format_verifies_in_another()
    {
        var uow = new FakeUnitOfWork();
        var svc = Service(uow);

        await svc.RequestOtp(new RequestOtpInput { Phone = "0790000000" });
        var res = await svc.VerifyOtp(new VerifyOtpInput { Phone = "+962790000000", Code = "1234" });

        Assert.True(res.Success);
        Assert.True(res.Data!.IsNewUser);
        Assert.Equal("790000000", uow.Store<User>().Single().PhoneKey);
    }

    [Fact]
    public async Task Number_shorter_than_the_key_is_rejected()
    {
        var uow = new FakeUnitOfWork();

        var res = await Service(uow).RequestOtp(new RequestOtpInput { Phone = "07900" });

        Assert.False(res.Success);
        Assert.Empty(uow.Store<OtpCode>());
    }

    [Fact]
    public async Task Different_number_does_not_match_an_existing_account()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<User>().Add(new User
        {
            Id = 7, Phone = "+962790000000", PhoneKey = "790000000", PhoneVerified = true,
        });
        var svc = Service(uow);

        await svc.RequestOtp(new RequestOtpInput { Phone = "+962791111111" });
        var res = await svc.VerifyOtp(new VerifyOtpInput { Phone = "+962791111111", Code = "1234" });

        Assert.True(res.Success);
        Assert.True(res.Data!.IsNewUser);
        Assert.Equal(2, uow.Store<User>().Count);
    }
}
