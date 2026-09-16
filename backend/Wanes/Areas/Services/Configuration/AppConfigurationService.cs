using Microsoft.EntityFrameworkCore;
using Wanes.Areas.Services.Audit;
using Wanes.Areas.Services.Configuration.Models;
using Wanes.DataAccess.Repositories;
using Wanes.DataAccess.UnitOfWorks;
using Wanes.Areas.Domain.RiderTrips;
using Wanes.Areas.Domain.RideRequests;
using Wanes.Areas.Domain.Trips;
using Wanes.Shareds.Models;
using Entity = Wanes.Areas.Domain.Configuration.AppConfiguration;

namespace Wanes.Areas.Services.Configuration;

/// <summary>
/// Reads and writes the single platform settings row.
///
/// The row is seeded on startup, but <see cref="Current"/> still tolerates its
/// absence: the app fetches this before it can show anything, so a missing row
/// must degrade to the built-in defaults rather than fail the launch.
/// </summary>
public class AppConfigurationService : IAppConfigurationService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly IRepository<Entity> repository;

    public AppConfigurationService(
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IRepository<Entity> repository)
    {
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.repository = repository;
    }

    public async Task<BaseResponse<AppConfigurationOutput>> Get()
    {
        var entity = await Current() ?? new Entity();
        return new BaseResponse<AppConfigurationOutput>(new AppConfigurationOutput(entity));
    }

    public async Task<BaseResponse<AppConfigurationOutput>> Update(AppConfigurationInput input)
    {
        var entity = await Current();
        var isNew = entity == null;
        entity ??= new Entity();

        var before = new AppConfigurationOutput(entity);

        entity.CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant();
        entity.CurrencySymbol = input.CurrencySymbol.Trim();
        entity.CurrencyPosition = input.CurrencyPosition;
        entity.CurrencyDecimals = input.CurrencyDecimals;
        entity.PrimaryColor = NormalizeHex(input.PrimaryColor);
        entity.FontFamily = input.FontFamily;
        // Clamped as well as validated, here and below: the attributes answer
        // the CMS, these answer everything else that can reach the row.
        entity.ConfirmCutoffMinutes = Math.Clamp(input.ConfirmCutoffMinutes,
            TripConfirmationRules.MinCutoffMinutes, TripConfirmationRules.MaxCutoffMinutes);
        entity.ConfirmDecisionLeadMinutes = Math.Clamp(input.ConfirmDecisionLeadMinutes,
            TripConfirmationRules.MinDecisionLeadMinutes, TripConfirmationRules.MaxDecisionLeadMinutes);
        entity.MinimumPassengersDefault =
            TripConfirmationRules.DefaultThreshold(input.MinimumPassengersDefault);
        entity.DriverSelectionWindowMinutes =
            DriverSelectionRules.WindowFor(input.DriverSelectionWindowMinutes);
        entity.AverageSpeedKmh = RiderTripRules.SpeedFor(input.AverageSpeedKmh);
        entity.FareBaseAmount = FareRules.RateFor(input.FareBaseAmount);
        entity.FarePerKm = FareRules.RateFor(input.FarePerKm);
        entity.SupportPhone = Blank(input.SupportPhone);
        entity.SupportWhatsApp = Blank(input.SupportWhatsApp);
        entity.SupportEmail = Blank(input.SupportEmail)?.ToLowerInvariant();
        entity.SupportWebsite = Blank(input.SupportWebsite);
        entity.SupportHours = Blank(input.SupportHours);

        if (isNew)
        {
            repository.Create(entity);
        }
        else
        {
            entity.ModificationDate = DateTime.UtcNow;
            repository.Update(entity);
        }

        await unitOfWork.SaveAsync();
        await auditService.LogAsync("admin.configuration.update", nameof(Entity), entity.Id,
            before, new AppConfigurationOutput(entity));

        return new BaseResponse<AppConfigurationOutput>(new AppConfigurationOutput(entity));
    }

    /// <summary>Oldest live row wins, so a stray duplicate can never flip the brand mid-flight.</summary>
    private Task<Entity?> Current() =>
        repository.Query().OrderBy(x => x.Id).FirstOrDefaultAsync();

    /// <summary>
    /// Trimmed, or null when the admin cleared the field. Collapsing `""` to null
    /// keeps "unset" a single value, so a client only has to test for one.
    /// </summary>
    private static string? Blank(string? raw)
    {
        var text = raw?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>`#abc` / `abc123` → `#AABBCC`, so clients parse exactly one shape.</summary>
    private static string NormalizeHex(string raw)
    {
        var hex = raw.Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        return $"#{hex.ToUpperInvariant()}";
    }
}
