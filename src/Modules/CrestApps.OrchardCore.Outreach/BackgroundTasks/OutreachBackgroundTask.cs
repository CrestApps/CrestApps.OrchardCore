using CrestApps.OrchardCore.Outreach.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Outreach.BackgroundTasks;

/// <summary>
/// Every minute, sends the outreach batches that are due, each in its own scope. Several nodes may run it at once: the
/// leased work queue gives every batch to one of them.
/// </summary>
[BackgroundTask(
    Title = "Outreach",
    Schedule = "* * * * *",
    Description = "Sends due outreach batches: broadcasts and campaigns, one page of recipients at a time.",
    LockTimeout = 3_000,
    LockExpiration = 600_000)]
public sealed class OutreachBackgroundTask : IBackgroundTask
{
    // Batches a run of the task sends, so one big run doesn't hold the task.
    private const int BatchesPerRun = 100;

    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<OutreachBatchProcessor>().ProcessDueAsync(BatchesPerRun, cancellationToken);
}
