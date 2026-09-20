using CrestApps.Core.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Folds appended event count contributions into the daily totals they belong to. Counting is appended rather
/// than accumulated in place so that concurrent writers never contend for one row, which leaves the folding to
/// be done afterwards by a single roller. The background task lock is what makes it single-writer across
/// nodes, so the totals are only ever updated from one place at a time.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Metric Rollup",
    Schedule = "* * * * *",
    Description = "Folds appended Contact Center event counts into their daily totals.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class ContactCenterMetricRollupBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IContactCenterMetricRollupCycle>().RunAsync(cancellationToken);
}
