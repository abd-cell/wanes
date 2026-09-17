using Wanes.Shareds.Hosting;

namespace Wanes.Areas.Services.Series;

/// <summary>
/// The series clocks: offers a rider left unanswered, commitments that have
/// run their course, and the week-ahead summary. Every five minutes — the
/// deadlines are hours away and the summary is once a week.
/// </summary>
public class SeriesWorker : PeriodicWorker
{
    public SeriesWorker(IServiceScopeFactory scopeFactory, ILogger<SeriesWorker> logger)
        : base(scopeFactory, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

    protected override string WorkDescription => "Series swept";

    protected override async Task<int> RunAsync(IServiceProvider services, CancellationToken stoppingToken)
    {
        var series = services.GetRequiredService<ISeriesService>();
        var decided = await series.DecideDue();
        var closed = await series.CloseFinished();
        var summaries = await series.SendWeeklySummaries();
        return decided + closed + summaries;
    }
}
