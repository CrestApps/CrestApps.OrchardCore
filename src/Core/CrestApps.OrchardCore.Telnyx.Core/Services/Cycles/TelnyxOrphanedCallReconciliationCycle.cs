using CrestApps.Core.Hosting.Background;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telnyx.Core.Services;

/// <summary>
/// Asks the provider what calls it actually has up, and acts on the ones this platform has no record of.
/// </summary>
public sealed class TelnyxOrphanedCallReconciliationCycle : ITelnyxOrphanedCallReconciliationCycle
{
    private readonly TelnyxOrphanedCallReconciler _reconciler;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxOrphanedCallReconciliationCycle"/> class.
    /// </summary>
    /// <param name="reconciler">The reconciler.</param>
    /// <param name="logger">The logger.</param>
    public TelnyxOrphanedCallReconciliationCycle(
        TelnyxOrphanedCallReconciler reconciler,
        ILogger<TelnyxOrphanedCallReconciliationCycle> logger)
    {
        _reconciler = reconciler;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _reconciler.ReconcileAsync(cancellationToken);

            if (result.OrphansFound > 0 && _logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
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
            _logger.LogError(ex, "An error occurred while reconciling orphaned Telnyx calls.");
        }
    }
}
