namespace Wanes.Areas.Services.Requests;

/// <summary>
/// Sweeps expired hails on a timer.
///
/// A hail's TTL used to be enforced only by the queries that read it — every
/// list filtered on <c>ExpiresAt &gt; now</c>, so an old request quietly stopped
/// appearing but stayed <c>Open</c> forever. That was enough for the lists and
/// wrong for everything else: the row never reached a terminal status, nothing
/// ever told a driver already holding the card that it was over, and a driver
/// could still accept a request whose window had closed. Flipping the status in
/// one place fixes all three, and the broadcast that goes with it is what
/// actually removes the card from the driver's screen.
///
/// Registered by hand in <c>Program.cs</c>: the <c>[ScopedInjectable]</c>
/// convention wires service interfaces, and a hosted service is neither.
/// </summary>
public class RideRequestExpiryWorker : BackgroundService
{
    /// <summary>
    /// How often to look. Not configurable, unlike the TTL itself: this is the
    /// granularity of the sweep, not a product decision, and it only has to be
    /// short enough that "expired" and "gone from the screen" feel simultaneous.
    /// </summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<RideRequestExpiryWorker> logger;

    public RideRequestExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<RideRequestExpiryWorker> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        // One sweep before the first tick, so a restart clears whatever expired
        // while the process was down instead of leaving it for half a minute.
        await Sweep(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await Sweep(stoppingToken);
    }

    private async Task Sweep(CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested) return;
        try
        {
            // The services are scoped (they hold a DbContext); a hosted service is
            // a singleton, so each sweep gets its own scope.
            using var scope = scopeFactory.CreateScope();
            var requests = scope.ServiceProvider.GetRequiredService<IRideRequestService>();

            var expired = await requests.ExpireDue();
            if (expired > 0) logger.LogInformation("Expired {Count} ride request(s).", expired);
        }
        catch (Exception ex)
        {
            // A failed sweep must not take the worker down — the next tick retries.
            logger.LogError(ex, "Sweeping expired ride requests failed.");
        }
    }
}
