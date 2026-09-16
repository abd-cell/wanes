using Wanes.Shareds.Hosting;

namespace Wanes.Areas.Services.RideRequests;

/// <summary>
/// The three clocks a ride request runs on, in the order they matter.
///
/// <list type="number">
/// <item><b>Notify</b> — a request that has come within the push horizon
/// reaches nearby drivers' phones. It was on the board from the moment it was
/// written; this is only about interrupting somebody.</item>
/// <item><b>Select</b> — a request whose offer window has closed gets a driver.
/// Does nothing while selection is immediate, which is the shipped
/// configuration.</item>
/// <item><b>Expire</b> — a request whose departure came and went with no driver
/// is closed, and the card disappears from every screen holding it.</item>
/// </list>
///
/// Notify before select before expire, deliberately. A request that entered the
/// horizon and departed inside one interval should still have reached somebody
/// before it is written off, and a window that closed in the same tick should be
/// decided before the departure clock is allowed to void it.
///
/// On a timer rather than at read time because every one of these is about
/// people who are not looking at the app: a driver who never opens it still has
/// to be told there is demand near them, and riders waiting on a request nobody
/// took still have to learn it is over.
/// </summary>
public class RideRequestSweepWorker : PeriodicWorker
{
    public RideRequestSweepWorker(IServiceScopeFactory scopeFactory,
        ILogger<RideRequestSweepWorker> logger)
        : base(scopeFactory, logger) { }

    protected override TimeSpan Interval => TimeSpan.FromSeconds(30);

    protected override string WorkDescription => "Ride requests swept";

    protected override async Task<int> RunAsync(IServiceProvider services, CancellationToken stoppingToken)
    {
        var requests = services.GetRequiredService<IRideRequestService>();
        var interests = services.GetRequiredService<IDriverInterestService>();

        var notified = await requests.NotifyDue();
        var matched = await interests.SelectDue();
        var expired = await requests.ExpireDue();
        return notified + matched + expired;
    }
}
