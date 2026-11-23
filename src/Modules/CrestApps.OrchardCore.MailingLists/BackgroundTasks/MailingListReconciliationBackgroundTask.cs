using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.MailingLists.Core.Sync;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.MailingLists.BackgroundTasks;

/// <summary>
/// Reads, once a day, the changes of each list whose provider reports them, so a change a dropped webhook missed still
/// reaches the site.
/// </summary>
[BackgroundTask(
    Title = "Reconcile mailing lists with providers",
    Schedule = "40 3 * * *",
    Description = "Reads the member changes of each list kept at an outside provider since the last run, and applies them.",
    LockTimeout = 1_000,
    LockExpiration = 3_600_000)]
public sealed class MailingListReconciliationBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run => run.AddItems(await serviceProvider.GetRequiredService<MailingListReconciliation>().RunAsync(cancellationToken), "applied"));
}
