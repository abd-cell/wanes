namespace Wanes.Shareds.Hosting;

/// <summary>
/// The plumbing every sweeper in this codebase needs, so none of them has to
/// own it: a timer, a fresh dependency-injection scope per pass, a sweep before
/// the first tick, and a failure that costs one pass rather than the worker.
///
/// A sweeper's own file is then just the interesting part — how often, and what
/// one pass does. Four of them existed as near-identical copies of this loop
/// before it was pulled out, and the copies had already begun to differ in the
/// two places that matter: whether an exception killed the timer, and whether a
/// restart caught up on what expired while the process was down.
///
/// Why a scope per pass: the services these call are scoped because they hold a
/// <c>DbContext</c>, and a hosted service is a singleton. Resolving one at
/// construction would pin a single context open for the life of the process,
/// and its change tracker would grow forever.
/// </summary>
public abstract class PeriodicWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger logger;

    protected PeriodicWorker(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    /// <summary>
    /// How often to look. This is the granularity of the sweep, not a product
    /// decision — the deadlines themselves are configuration — so it only has
    /// to be short enough that "due" and "done" feel simultaneous.
    /// </summary>
    protected abstract TimeSpan Interval { get; }

    /// <summary>What to say when a pass did something. Keeps the logs readable.</summary>
    protected abstract string WorkDescription { get; }

    /// <summary>
    /// One pass, against the given scope. Returns how many rows it touched;
    /// zero is the ordinary answer and is not logged.
    /// </summary>
    protected abstract Task<int> RunAsync(IServiceProvider services, CancellationToken stoppingToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        // One pass before the first tick, so a restart clears whatever came due
        // while the process was down instead of leaving it for another interval.
        await Sweep(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await Sweep(stoppingToken);
    }

    private async Task Sweep(CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested) return;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var touched = await RunAsync(scope.ServiceProvider, stoppingToken);
            if (touched > 0) logger.LogInformation("{Work}: {Count}.", WorkDescription, touched);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown, not a fault.
        }
        catch (Exception ex)
        {
            // A failed pass must not take the worker down — the next tick retries.
            logger.LogError(ex, "{Work} failed.", WorkDescription);
        }
    }
}
