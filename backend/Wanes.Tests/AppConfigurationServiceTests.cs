using Wanes.Areas.Services.Configuration;
using Wanes.Areas.Services.Configuration.Models;
using Wanes.Shareds.Enums;
using Wanes.Tests.TestDoubles;
using Entity = Wanes.Areas.Domain.Configuration.AppConfiguration;

namespace Wanes.Tests;

/// <summary>
/// The platform settings row, and the two things that make it unusual.
///
/// It is a singleton the clients read before sign-in, so <c>Get</c> has to
/// answer even when the row is missing — the app cannot launch without a brand
/// and a typeface. And every save replaces the whole row, so a field added
/// later is only actually wired if it survives a round trip; the font is
/// checked here for exactly that reason.
/// </summary>
public class AppConfigurationServiceTests
{
    private static AppConfigurationService Service(FakeUnitOfWork uow, FakeAuditService? audit = null) =>
        new(uow, audit ?? new FakeAuditService(), uow.Repository<Entity>());

    /// <summary>A payload that changes every knob away from its default.</summary>
    private static AppConfigurationInput Input(AppFont font = AppFont.Inter) => new()
    {
        CurrencyCode = "usd",
        CurrencySymbol = "$",
        CurrencyPosition = CurrencyPosition.Before,
        CurrencyDecimals = 2,
        PrimaryColor = "#abc",
        FontFamily = font,
        HailRequestTtlMinutes = 25,
    };

    [Fact]
    public async Task Get_without_a_row_returns_the_shipped_defaults()
    {
        var result = await Service(new FakeUnitOfWork()).Get();

        Assert.True(result.Success);
        // Not merely "some font": the app paints this before it has ever
        // reached the server, so the default has to be the shipped design.
        Assert.Equal(AppFont.Jakarta, result.Data!.FontFamily);
        Assert.Equal("#0FAE9E", result.Data.PrimaryColor);
    }

    [Fact]
    public async Task Update_persists_the_chosen_font()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<Entity>().Add(new Entity());

        var result = await Service(uow).Update(Input(AppFont.Tajawal));

        Assert.True(result.Success);
        Assert.Equal(AppFont.Tajawal, result.Data!.FontFamily);
        Assert.Equal(AppFont.Tajawal, uow.Store<Entity>().Single().FontFamily);
    }

    [Fact]
    public async Task Update_leaves_the_font_readable_by_a_client_when_the_row_is_created()
    {
        // No seeded row: Update has to create one rather than fail, and the
        // font it writes must still be a value every client can resolve.
        var uow = new FakeUnitOfWork();

        var result = await Service(uow).Update(Input(AppFont.System));

        Assert.True(result.Success);
        Assert.Equal(AppFont.System, uow.Store<Entity>().Single().FontFamily);
        Assert.Contains(result.Data!.FontFamily, Enum.GetValues<AppFont>());
    }

    [Fact]
    public async Task Update_normalises_the_rest_of_the_row_and_audits_once()
    {
        var uow = new FakeUnitOfWork();
        uow.Store<Entity>().Add(new Entity());
        var audit = new FakeAuditService();

        var result = await Service(uow, audit).Update(Input());

        // The hex shorthand and the lower-case code are expanded on save, so a
        // client only ever parses one shape.
        Assert.Equal("#AABBCC", result.Data!.PrimaryColor);
        Assert.Equal("USD", result.Data.CurrencyCode);
        Assert.Equal(AppFont.Inter, result.Data.FontFamily);
        Assert.Single(audit.Actions);
    }
}
