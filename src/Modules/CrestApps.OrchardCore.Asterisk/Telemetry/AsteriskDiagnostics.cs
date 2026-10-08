using System.Diagnostics.Metrics;

namespace CrestApps.OrchardCore.Asterisk.Telemetry;

/// <summary>
/// Defines the stable metrics contract for the Asterisk provider. The <see cref="Meter"/> name is a public
/// integration surface that operators subscribe to from an OpenTelemetry exporter, so it must not change without
/// a documented migration. Instruments are process-wide and thread-safe by design, following the standard
/// <see cref="System.Diagnostics.Metrics"/> pattern.
/// </summary>
public static class AsteriskDiagnostics
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.Metrics.Meter"/> that emits Asterisk provider metrics.
    /// </summary>
    public const string MeterName = "CrestApps.OrchardCore.Asterisk";

    private static readonly Meter _meter = new(MeterName);

    private static readonly Counter<long> _realtimeIngestionSaturated = _meter.CreateCounter<long>(
        "asterisk.realtime.ingestion.saturated",
        unit: "{episode}",
        description: "The number of times the Asterisk real-time ingestion buffer filled and applied backpressure to the provider event stream.");

    private static readonly Counter<long> _realtimeConnected = _meter.CreateCounter<long>(
        "asterisk.realtime.connected",
        unit: "{connection}",
        description: "The number of times the Asterisk real-time voice listener successfully established its ARI event-stream connection, counting both the first connection and every reconnection.");

    private static readonly Counter<long> _realtimeReconnectAttempted = _meter.CreateCounter<long>(
        "asterisk.realtime.reconnect_attempted",
        unit: "{attempt}",
        description: "The number of times the Asterisk real-time voice listener re-entered its loop to re-establish the ARI event-stream connection after a prior connection ended or a connect attempt failed. A non-zero rate signals connection churn.");

    /// <summary>
    /// Records that the real-time ingestion buffer for a provider filled and began applying backpressure. This is
    /// recorded once per saturation episode — on the first wait — not on every full write and not on a drop.
    /// </summary>
    /// <param name="providerName">The provider technical name whose ingestion buffer saturated, used as a low-cardinality metric dimension.</param>
    public static void RecordRealtimeIngestionSaturated(string providerName)
    {
        _realtimeIngestionSaturated.Add(1, new KeyValuePair<string, object>("provider", providerName ?? "unspecified"));
    }

    /// <summary>
    /// Records that the real-time voice listener successfully established its ARI event-stream connection. This is
    /// recorded once per successful connect, so the first connection and every reconnection each count once.
    /// </summary>
    /// <param name="providerName">The provider technical name that connected, used as a low-cardinality metric dimension.</param>
    public static void RecordRealtimeConnected(string providerName)
    {
        _realtimeConnected.Add(1, new KeyValuePair<string, object>("provider", providerName ?? "unspecified"));
    }

    /// <summary>
    /// Records that the real-time voice listener re-entered its loop to re-establish the ARI event-stream
    /// connection after a prior connection ended or a connect attempt failed. This is recorded once per reconnect
    /// attempt, before the backoff delay, so a rising rate indicates connection churn independent of whether the
    /// reconnect ultimately succeeds.
    /// </summary>
    /// <param name="providerName">The provider technical name that is reconnecting, used as a low-cardinality metric dimension.</param>
    public static void RecordRealtimeReconnectAttempted(string providerName)
    {
        _realtimeReconnectAttempted.Add(1, new KeyValuePair<string, object>("provider", providerName ?? "unspecified"));
    }
}
