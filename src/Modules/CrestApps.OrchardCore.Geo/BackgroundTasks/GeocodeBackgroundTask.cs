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
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IGeocodeQueue>().ProcessDueAsync(cancellationToken: cancellationToken);
}
