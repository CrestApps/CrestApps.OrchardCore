using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Timeline.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Timeline.BackgroundTasks;

/// <summary>
/// Removes, every hour, the files that no timeline entry kept a day after they were stored, such as those of a request
/// that failed after storing them (<see cref="TimelineOrphanFileSweeper"/>).
/// </summary>
[BackgroundTask(
    Title = "Remove files no timeline entry kept",
    Schedule = "17 * * * *",
    Description = "Removes the files of records with a timeline that no entry kept a day after they were stored.",
    LockTimeout = 1_000,
    LockExpiration = 600_000)]
public sealed class TimelineOrphanFilesBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run => run.AddItems(await serviceProvider.GetRequiredService<TimelineOrphanFileSweeper>().SweepAsync(cancellationToken), "removed"));
}
