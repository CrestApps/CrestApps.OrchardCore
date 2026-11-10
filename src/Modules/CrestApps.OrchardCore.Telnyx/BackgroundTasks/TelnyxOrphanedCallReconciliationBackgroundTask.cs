using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;

namespace CrestApps.OrchardCore.Telnyx.BackgroundTasks;

/// <summary>
/// Asks the provider what calls it actually has up, and acts on the ones this platform has no record of.
/// </summary>
/// <remarks>
/// The interaction reconciliation task walks the platform's own records, so it can only repair calls there is a
/// record of. This is the other direction, and it is the only way to see a call that was placed immediately
/// before a restart: no interaction was written, so no local sweep will ever reach it, and the person on it is
/// connected to a platform that does not know they are there.
/// </remarks>
[BackgroundTask(
    Title = "Telnyx Orphaned Call Reconciliation",
    Schedule = "*/5 * * * *",
    Description = "Finds calls the Telnyx connection has up that this platform has no interaction for.",
    LockTimeout = 5_000,
    LockExpiration = 120_000)]
public sealed class TelnyxOrphanedCallReconciliationBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(run => ReconcileAsync(serviceProvider, run, cancellationToken));

    private static async Task ReconcileAsync(IServiceProvider serviceProvider, MeasuredTaskRun run, CancellationToken cancellationToken)
    {
        var reconciler = serviceProvider.GetRequiredService<TelnyxOrphanedCallReconciler>();
        var logger = serviceProvider.GetRequiredService<ILogger<TelnyxOrphanedCallReconciliationBackgroundTask>>();

        try
        {
            var result = await reconciler.ReconcileAsync(cancellationToken);

            // Not configured yet, or the listing was refused: nothing was reconciled this pass.
            if (!result.Succeeded)
            {
                run.Skip();
            }

            run.AddItems(result.OrphansEnded, "ended");
            run.AddItems(result.OrphansFound - result.OrphansEnded, "left_up");

            if (result.OrphansFound > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Orphaned-call reconciliation found {Found} live Telnyx calls with no interaction and ended {Ended}.",
                    result.OrphansFound,
                    result.OrphansEnded);
            }
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            // The tenant is shutting down; stop quietly rather than logging the cancellation as a failure.
            run.ReportError(ex);
        }
        catch (Exception ex)
        {
            run.ReportError(ex);
            logger.LogError(ex, "An error occurred while reconciling orphaned Telnyx calls.");
        }
    }
}
