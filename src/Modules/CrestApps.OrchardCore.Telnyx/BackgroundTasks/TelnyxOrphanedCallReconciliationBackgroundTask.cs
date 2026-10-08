using CrestApps.OrchardCore.Telnyx.Services;
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
    public async Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var reconciler = serviceProvider.GetRequiredService<TelnyxOrphanedCallReconciler>();
        var logger = serviceProvider.GetRequiredService<ILogger<TelnyxOrphanedCallReconciliationBackgroundTask>>();

        try
        {
            var result = await reconciler.ReconcileAsync(cancellationToken);

            if (result.OrphansFound > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Orphaned-call reconciliation found {Found} live Telnyx calls with no interaction and ended {Ended}.",
                    result.OrphansFound,
                    result.OrphansEnded);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The tenant is shutting down; stop quietly rather than logging the cancellation as a failure.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while reconciling orphaned Telnyx calls.");
        }
    }
}
