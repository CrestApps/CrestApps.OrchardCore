using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.ServiceLevels.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ServiceLevels.BackgroundTasks;

/// <summary>
/// Raises service level warnings and breaches every minute.
/// </summary>
[BackgroundTask(
    Title = "Service levels",
    Schedule = "* * * * *",
    Description = "Raises a warning when a record is close to missing a service level target, and a breach when it misses it, once each.",
    LockTimeout = 1_000,
    LockExpiration = 300_000)]
public sealed class ServiceLevelMonitorBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run => run.AddItems(await serviceProvider.GetRequiredService<ServiceLevelMonitor>().CheckAsync(cancellationToken), "raised"));
}
