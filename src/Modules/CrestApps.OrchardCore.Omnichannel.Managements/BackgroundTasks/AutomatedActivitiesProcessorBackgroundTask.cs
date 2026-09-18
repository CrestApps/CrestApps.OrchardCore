using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;

[BackgroundTask(
    Title = "Omnichannel Automated Activities Processor",
    Schedule = "*/5 * * * *",
    Description = "Processes omnichannel activities.",
    LockTimeout = 5_000,
    LockExpiration = _leaseMilliseconds)]

/// <summary>
/// Represents the automated activities processor background task.
/// </summary>
public sealed class AutomatedActivitiesProcessorBackgroundTask : IBackgroundTask
{
    private const int _leaseMilliseconds = 600_000;
    // The lease is a compile-time attribute argument, so it stays a constant; everything else the pass does is
    // read from options at run time.


    /// <summary>
    /// Asynchronously performs the do work operation.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IAutomatedActivitiesProcessorCycle>().RunAsync(cancellationToken);
}
