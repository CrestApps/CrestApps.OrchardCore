using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Geo.BackgroundTasks;

/// <summary>
/// Geocodes the addresses that are due.
/// </summary>
[BackgroundTask(
    Title = "Geocode addresses",
    Schedule = "* * * * *",
    Description = "Finds the coordinates of addresses saved since the last run, and retries those that failed.",
    LockTimeout = 1_000,
    LockExpiration = 300_000)]
public sealed class GeocodeBackgroundTask : IBackgroundTask
{
    private const int BatchSize = 50;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        return this.RunAsync(async run =>
        {
            var processed = await serviceProvider.GetRequiredService<IGeocodeQueue>().ProcessDueAsync(BatchSize, cancellationToken);

            run.AddItems(processed);

            if (processed >= BatchSize)
            {
                // A full batch means more requests are due than one run takes: the queue is backing up.
                run.BudgetExceeded();
            }
        });
    }
}
