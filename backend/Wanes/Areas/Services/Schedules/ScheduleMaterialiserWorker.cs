using Wanes.Shareds.Hosting;

namespace Wanes.Areas.Services.Schedules;

/// <summary>
/// Turns schedules into real trips and postings, a rolling fortnight at a time.
///
/// Every quarter of an hour rather than every thirty seconds: the horizon is two
/// weeks out, so nothing here is urgent, and the pass touches every live
/// schedule. The one case that wants promptness — a schedule written this
/// morning for this evening — is still served inside the same quarter hour.
///
/// Registered by hand in <c>Program.cs</c>: the <c>[ScopedInjectable]</c>
/// convention wires service interfaces, and a hosted service is neither.
/// </summary>
public class ScheduleMaterialiserWorker : PeriodicWorker
{
    public ScheduleMaterialiserWorker(
        IServiceScopeFactory scopeFactory, ILogger<ScheduleMaterialiserWorker> logger)
        : base(scopeFactory, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(15);

    protected override string WorkDescription => "Scheduled occurrences generated";

    protected override async Task<int> RunAsync(IServiceProvider services, CancellationToken stoppingToken)
    {
        var schedules = services.GetRequiredService<ITripScheduleService>();
        return await schedules.MaterialiseDue();
    }
}
