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
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(run => ReconcileAsync(serviceProvider, run, cancellationToken));

    private static async Task ReconcileAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        var synchronizationService = serviceProvider.GetRequiredService<ITelephonyInteractionSynchronizationService>();
        var logger = serviceProvider.GetRequiredService<ILogger<TelephonyInteractionReconciliationBackgroundTask>>();

        try
        {
            var corrected = await synchronizationService.ReconcileActiveInteractionsAsync(cancellationToken);

            run.AddItems(corrected, "corrected");
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            // The tenant is shutting down; stop quietly instead of logging the cancellation as a reconciliation failure.
            run.ReportError(ex);
        }
        catch (Exception ex)
        {
            run.ReportError(ex);
            logger.LogError(ex, "An error occurred while reconciling telephony interaction state.");
        }
    }
}
