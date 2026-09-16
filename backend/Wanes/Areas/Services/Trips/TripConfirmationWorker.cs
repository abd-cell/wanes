using Wanes.Shareds.Hosting;

namespace Wanes.Areas.Services.Trips;

/// <summary>
/// Watches the seat thresholds: asks the driver when their decision comes due,
/// and answers for them — by calling the trip off — when the deadline passes in
/// silence.
///
/// The pair has to run on a timer rather than at read time because both ends of
/// it are about people who are not looking at the app. A driver who never opens
/// it still has to be asked; riders holding seats on a trip that will not run
/// still have to be told, in time to find another ride.
///
/// Prompt before resolve, in that order, so a trip whose window opened and
/// closed inside one interval is still asked about before it is cancelled — the
/// driver gets the notification and can act on it, rather than only learning
/// afterwards that their trip is gone.
/// </summary>
public class TripConfirmationWorker : PeriodicWorker
{
    public TripConfirmationWorker(IServiceScopeFactory scopeFactory, ILogger<TripConfirmationWorker> logger)
        : base(scopeFactory, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override string WorkDescription => "Seat thresholds resolved";

    protected override async Task<int> RunAsync(IServiceProvider services, CancellationToken stoppingToken)
    {
        var confirmations = services.GetRequiredService<ITripConfirmationService>();

        var prompted = await confirmations.PromptDue();
        var resolved = await confirmations.ResolveDue();
        return prompted + resolved;
    }
}
