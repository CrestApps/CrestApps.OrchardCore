using CrestApps.Core.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.ContactCenter.BackgroundTasks;

/// <summary>
/// Revalidates active provider-backed interactions against the telephony server so restarts and missed
/// live events do not leave queued voice work out of sync.
/// </summary>
[BackgroundTask(
    Title = "Contact Center Provider Call Reconciliation",
    Schedule = "* * * * *",
    Description = "Reconciles active Contact Center voice interactions against current provider call state.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class ProviderCallStateReconciliationBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<IProviderCallStateReconciliationCycle>().RunAsync(cancellationToken);
}
