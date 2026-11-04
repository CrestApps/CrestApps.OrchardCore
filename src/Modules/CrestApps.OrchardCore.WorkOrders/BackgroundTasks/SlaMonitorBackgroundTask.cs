using CrestApps.OrchardCore.WorkOrders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.WorkOrders.BackgroundTasks;

/// <summary>
/// Raises service level warnings and breaches.
/// </summary>
[BackgroundTask(
    Title = "Work order service levels",
    Schedule = "* * * * *",
    Description = "Raises a warning when a work order is close to missing its response or resolution target, and a breach when it misses it, once each.",
    LockTimeout = 1_000,
    LockExpiration = 300_000)]
public sealed class SlaMonitorBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<SlaMonitor>().CheckAsync(cancellationToken);
}
