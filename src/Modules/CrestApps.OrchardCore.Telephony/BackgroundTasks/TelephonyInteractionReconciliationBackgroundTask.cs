using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var synchronizationService = serviceProvider.GetRequiredService<ITelephonyInteractionSynchronizationService>();
        var logger = serviceProvider.GetRequiredService<ILogger<TelephonyInteractionReconciliationBackgroundTask>>();

        try
        {
            await synchronizationService.ReconcileActiveInteractionsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(OperationalLogRedactor.RedactException(ex), "An error occurred while reconciling telephony interaction state.");
        }
    }
}
