using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Drains the real-time ingestion buffer after the socket disconnects. A dispatcher that keeps clearing events is
/// allowed to finish, up to an overall budget, so a healthy but slow drain is not truncated; a stalled dispatch is
/// abandoned so it cannot hold the listener back from reconnecting. Events abandoned here are recovered for known
/// calls by reconciliation on the next connect, though a dispatch wedged in non-cancellable work can still delay
/// the reconnect until it returns.
/// </summary>
internal static class AsteriskRealtimeIngestionDrainer
{
    /// <summary>
    /// Waits for the buffered-event worker to finish draining, bounded by both drain progress and an overall budget.
    /// </summary>
    /// <param name="worker">The task draining and dispatching buffered events.</param>
    /// <param name="reader">The buffer reader, used to detect whether the drain is still making progress.</param>
    /// <param name="workerCancellation">The worker's cancellation source, cancelled when a stalled drain is abandoned.</param>
    /// <param name="progressWindow">How long to wait for the buffer to shrink before re-checking progress.</param>
    /// <param name="maxDrainDuration">The overall ceiling on the drain before a still-buffered dispatch is abandoned.</param>
    /// <param name="providerName">The provider technical name, used for logging.</param>
    /// <param name="logger">The logger used to surface an abandoned drain.</param>
    /// <param name="cancellationToken">A token that aborts the drain when the listener is shutting down.</param>
    public static async Task DrainAsync(
        Task worker,
        ChannelReader<string> reader,
        CancellationTokenSource workerCancellation,
        TimeSpan progressWindow,
        TimeSpan maxDrainDuration,
        string providerName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var drainStopwatch = Stopwatch.StartNew();

        try
        {
            while (true)
            {
                var remaining = maxDrainDuration - drainStopwatch.Elapsed;

                if (remaining <= TimeSpan.Zero)
                {
                    await AbandonAsync(worker, reader, workerCancellation, $"{maxDrainDuration} budget exhausted", providerName, logger);

                    return;
                }

                var window = remaining < progressWindow ? remaining : progressWindow;
                var bufferedBefore = reader.CanCount ? reader.Count : -1;

                try
                {
                    await worker.WaitAsync(window, cancellationToken);

                    return;
                }
                catch (TimeoutException)
                {
                    var bufferedAfter = reader.CanCount ? reader.Count : -1;

                    // Keep waiting while the dispatcher is still clearing the buffer; abandon only a stalled drain.
                    if (reader.CanCount && bufferedAfter < bufferedBefore)
                    {
                        continue;
                    }

                    await AbandonAsync(worker, reader, workerCancellation, $"no progress within {progressWindow}", providerName, logger);

                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            await ObserveAsync(worker);
        }
    }

    private static async Task AbandonAsync(
        Task worker,
        ChannelReader<string> reader,
        CancellationTokenSource workerCancellation,
        string reason,
        string providerName,
        ILogger logger)
    {
        var stillBuffered = reader.CanCount
            ? (object)reader.Count
            : "an unknown number of";

        logger.LogWarning(
            "The Asterisk real-time ingestion buffer for provider {ProviderName} stopped draining with {BufferedEvents} event(s) still buffered ({Reason}); abandoning them and reconnecting to reconcile.",
            providerName,
            stillBuffered,
            reason);

        await workerCancellation.CancelAsync();
        await ObserveAsync(worker);
    }

    private static async Task ObserveAsync(Task worker)
    {
        try
        {
            await worker;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
