using CrestApps.OrchardCore.Telephony.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Telephony.BackgroundTasks;

/// <summary>
/// Periodically reconciles in-progress telephony interactions with provider-authoritative state.
/// </summary>
[BackgroundTask(
    Title = "Telephony Interaction Reconciliation",
    Schedule = "* * * * *",
    Description = "Reconciles in-progress soft-phone calls against the current provider state.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class TelephonyInteractionReconciliationBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<ITelephonyInteractionReconciliationCycle>().RunAsync(cancellationToken);
}
