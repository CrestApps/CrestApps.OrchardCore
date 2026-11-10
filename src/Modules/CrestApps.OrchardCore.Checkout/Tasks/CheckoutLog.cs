using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Checkout.Tasks;

/// <summary>
/// The source-generated log messages of the checkout module, with event ids from 12000 to 12099.
/// </summary>
internal static partial class CheckoutLog
{
    /// <summary>
    /// Logs what a run of the checkout reconciliation sweep did.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="sessions">The number of checkouts found waiting on a provider or on their fulfillment.</param>
    /// <param name="completed">The number of them the sweep completed.</param>
    /// <param name="failed">The number of them the sweep failed to reconcile.</param>
    /// <param name="expired">The number of abandoned checkouts the sweep expired.</param>
    /// <param name="oldestPendingSeconds">The age, in seconds, of the oldest checkout found waiting.</param>
    [LoggerMessage(
        EventId = CrestAppsEventIds.Commerce,
        Level = LogLevel.Information,
        Message = "The checkout reconciliation sweep checked {Sessions} waiting checkouts: {Completed} completed, {Failed} failed to reconcile, {Expired} abandoned ones expired; the oldest had waited {OldestPendingSeconds:F0} seconds.")]
    public static partial void ReconciliationSwept(ILogger logger, int sessions, int completed, int failed, int expired, double oldestPendingSeconds);
}
