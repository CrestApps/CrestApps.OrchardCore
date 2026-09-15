using System.Threading.Channels;
using CrestApps.OrchardCore.Asterisk.Telemetry;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Writes real-time provider payloads into a bounded buffer, applying real backpressure when the buffer fills
/// instead of dropping the connection on the first full write. It records a saturation metric exactly once per
/// episode and reports when the buffer stayed full long enough that the caller should reconnect and reconcile.
/// </summary>
internal sealed class AsteriskRealtimeIngestionWriter
{
    private readonly ChannelWriter<string> _writer;
    private readonly ChannelReader<string> _reader;
    private readonly string _providerName;
    private readonly TimeSpan _backpressureTimeout;
    private readonly ILogger _logger;
    private bool _saturationSignaled;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskRealtimeIngestionWriter"/> class.
    /// </summary>
    /// <param name="writer">The bounded channel writer that buffers events for the dispatcher.</param>
    /// <param name="reader">The matching channel reader, used to detect when a saturation episode has fully drained.</param>
    /// <param name="providerName">The provider technical name, used for logging and the saturation metric dimension.</param>
    /// <param name="backpressureTimeout">How long to apply backpressure to a full buffer before reporting a reconnect.</param>
    /// <param name="logger">The logger used to surface saturation and reconnect signals.</param>
    public AsteriskRealtimeIngestionWriter(
        ChannelWriter<string> writer,
        ChannelReader<string> reader,
        string providerName,
        TimeSpan backpressureTimeout,
        ILogger logger)
    {
        _writer = writer;
        _reader = reader;
        _providerName = providerName;
        _backpressureTimeout = backpressureTimeout;
        _logger = logger;
    }

    /// <summary>
    /// Buffers a payload, absorbing backpressure within the configured window when the buffer is full.
    /// </summary>
    /// <param name="payload">The raw provider payload to buffer.</param>
    /// <param name="cancellationToken">A token that aborts the write when the listener is shutting down.</param>
    /// <returns>
    /// <see cref="AsteriskRealtimeIngestionWriteResult.Written"/> when the payload was buffered, or
    /// <see cref="AsteriskRealtimeIngestionWriteResult.BackpressureTimedOut"/> when the buffer stayed full for the
    /// whole backpressure window and the caller should reconnect and reconcile.
    /// </returns>
    public async Task<AsteriskRealtimeIngestionWriteResult> WriteAsync(string payload, CancellationToken cancellationToken)
    {
        // Fast path: while the buffer has room, enqueue without awaiting. Capture whether the buffer had fully
        // drained before this write so a recovered stream can end the saturation episode.
        var wasFullyDrained = !_reader.CanCount || _reader.Count == 0;

        if (_writer.TryWrite(payload))
        {
            // End the saturation episode only once the reader has fully caught up (the buffer was empty before this
            // write). Resetting on any single dequeue would over-count episodes while the buffer oscillates near
            // full, so a later fill is reported as a new episode only after a genuine recovery.
            if (wasFullyDrained)
            {
                _saturationSignaled = false;
            }

            return AsteriskRealtimeIngestionWriteResult.Written;
        }

        // The buffer is full. Record the saturation exactly once per episode — on the first wait, not on every
        // full write and not on a drop — so the signal counts episodes an operator can act on.
        if (!_saturationSignaled)
        {
            _saturationSignaled = true;
            AsteriskDiagnostics.RecordRealtimeIngestionSaturated(_providerName);

            _logger.LogWarning(
                "The Asterisk real-time ingestion buffer for provider {ProviderName} is saturated; applying backpressure to the provider event stream for up to {BackpressureTimeout}.",
                _providerName,
                _backpressureTimeout);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_backpressureTimeout);

        try
        {
            await _writer.WriteAsync(payload, timeoutCts.Token);

            return AsteriskRealtimeIngestionWriteResult.Written;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "The Asterisk real-time ingestion buffer for provider {ProviderName} stayed saturated for {BackpressureTimeout}; the listener will reconnect and reconcile provider state.",
                _providerName,
                _backpressureTimeout);

            return AsteriskRealtimeIngestionWriteResult.BackpressureTimedOut;
        }
    }
}
