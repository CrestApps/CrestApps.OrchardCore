using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using CrestApps.OrchardCore.Diagnostics;
using Polly.Timeout;

namespace CrestApps.OrchardCore.Asterisk.Telemetry;

/// <summary>
/// Defines the stable metrics contract for the Asterisk provider. The <see cref="Meter"/> name is a public
/// integration surface that operators subscribe to from an OpenTelemetry exporter, so it must not change without
/// a documented migration. Instruments are process-wide and thread-safe by design, following the standard
/// <see cref="System.Diagnostics.Metrics"/> pattern.
/// </summary>
/// <remarks>
/// The <c>asterisk.*</c> instruments are deprecated and are recorded for one more release next to their
/// <c>crestapps.asterisk.*</c> replacements, which follow the naming of every other CrestApps meter. The activity
/// source shares the meter's name.
/// </remarks>
public static class AsteriskDiagnostics
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.Metrics.Meter"/> that emits Asterisk provider metrics, and of
    /// the activity source of its spans.
    /// </summary>
    public const string MeterName = "CrestApps.OrchardCore.Asterisk";

    /// <summary>
    /// The tag that records the type of an ARI event, such as <c>StasisStart</c>, or <c>other</c> for a type that is
    /// not in <see cref="KnownEventTypes"/>.
    /// </summary>
    public const string EventTypeTag = "event_type";

    /// <summary>
    /// The event was mapped to a voice-state update and dispatched.
    /// </summary>
    public const string EventApplied = "applied";

    /// <summary>
    /// The event carried no voice-state update.
    /// </summary>
    public const string EventIgnored = "ignored";

    /// <summary>
    /// Dispatching the event failed.
    /// </summary>
    public const string EventFailed = "failed";

    /// <summary>
    /// The ARI request was answered with a 4xx status.
    /// </summary>
    public const string AriResultHttp4xx = "http_4xx";

    /// <summary>
    /// The ARI request was answered with a 5xx status.
    /// </summary>
    public const string AriResultHttp5xx = "http_5xx";

    /// <summary>
    /// Asterisk could not be reached.
    /// </summary>
    public const string AriResultUnreachable = "unreachable";

    /// <summary>
    /// The request asked whether something exists, and Asterisk answered 404: an answer, not a failure.
    /// </summary>
    public const string AriResultNotFound = "not_found";

    private static readonly Meter _meter = new(MeterName);

    private static readonly ConcurrentDictionary<RealtimeListenerTracking, byte> _listeners = new();

    private static readonly Counter<long> _realtimeIngestionSaturated = _meter.CreateCounter<long>(
        "asterisk.realtime.ingestion.saturated",
        unit: "{episode}",
        description: "Deprecated: use crestapps.asterisk.realtime.ingestion.saturated. The number of times the Asterisk real-time ingestion buffer filled and applied backpressure to the provider event stream.");

    private static readonly Counter<long> _realtimeConnected = _meter.CreateCounter<long>(
        "asterisk.realtime.connected",
        unit: "{connection}",
        description: "Deprecated: use crestapps.asterisk.realtime.connections. The number of times the Asterisk real-time voice listener successfully established its ARI event-stream connection, counting both the first connection and every reconnection.");

    private static readonly Counter<long> _realtimeReconnectAttempted = _meter.CreateCounter<long>(
        "asterisk.realtime.reconnect_attempted",
        unit: "{attempt}",
        description: "Deprecated: use crestapps.asterisk.realtime.reconnect_attempts. The number of times the Asterisk real-time voice listener re-entered its loop to re-establish the ARI event-stream connection after a prior connection ended or a connect attempt failed. A non-zero rate signals connection churn.");

    private static readonly Counter<long> _ingestionSaturated = _meter.CreateCounter<long>(
        "crestapps.asterisk.realtime.ingestion.saturated",
        unit: "{episode}",
        description: "The number of times the Asterisk real-time ingestion buffer filled and applied backpressure to the provider event stream.");

    private static readonly Counter<long> _connections = _meter.CreateCounter<long>(
        "crestapps.asterisk.realtime.connections",
        unit: "{connection}",
        description: "The number of times the Asterisk real-time voice listener established its ARI event-stream connection, the first connection and every reconnection.");

    private static readonly Counter<long> _reconnectAttempts = _meter.CreateCounter<long>(
        "crestapps.asterisk.realtime.reconnect_attempts",
        unit: "{attempt}",
        description: "The number of times the Asterisk real-time voice listener tried to re-establish its ARI event-stream connection.");

    private static readonly UpDownCounter<long> _connected = _meter.CreateUpDownCounter<long>(
        "crestapps.asterisk.realtime.connected",
        unit: "{connection}",
        description: "The number of Asterisk real-time voice listeners currently connected to their ARI event stream. Zero means the provider's call events are not being received.");

    private static readonly Counter<long> _events = _meter.CreateCounter<long>(
        "crestapps.asterisk.realtime.events",
        unit: "{event}",
        description: "The number of ARI events the Asterisk real-time voice listener received, by type and by what became of them.");

    private static readonly Histogram<double> _eventLag = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        _meter,
        "crestapps.asterisk.realtime.event.lag",
        "The time from an ARI event's own timestamp to the listener dispatching it.");

    private static readonly Histogram<double> _ariRequestDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        _meter,
        "crestapps.asterisk.ari.request.duration",
        "The duration of Asterisk ARI REST requests, until the response headers arrived.");

    // Read from the live listeners only: never a query in a gauge callback.
    private static readonly ObservableGauge<double> _lastEventAge = _meter.CreateObservableGauge(
        "crestapps.asterisk.realtime.last_event_age",
        ObserveLastEventAge,
        unit: "s",
        description: "The time since a connected Asterisk real-time voice listener last received an event, the longest across the provider's listeners.");

    private static readonly ObservableGauge<long> _queueDepth = _meter.CreateObservableGauge(
        "crestapps.asterisk.realtime.ingestion.queue_depth",
        ObserveQueueDepth,
        unit: "{event}",
        description: "The number of ARI events buffered for dispatch by the connected Asterisk real-time voice listeners.");

    /// <summary>
    /// The ARI event types recorded as themselves on <see cref="EventTypeTag"/>; any other type is recorded as <c>other</c>.
    /// </summary>
    public static IReadOnlySet<string> KnownEventTypes => _knownEventTypes;

    private static readonly HashSet<string> _knownEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "StasisStart",
        "StasisEnd",
        "ChannelCreated",
        "ChannelDestroyed",
        "ChannelStateChange",
        "ChannelHangupRequest",
        "ChannelHold",
        "ChannelUnhold",
        "ChannelEnteredBridge",
        "ChannelLeftBridge",
        "ChannelVarset",
        "ChannelDtmfReceived",
        "ChannelTalkingStarted",
        "ChannelTalkingFinished",
        "ChannelDialplan",
        "ChannelCallerId",
        "ChannelConnectedLine",
        "Dial",
        "BridgeCreated",
        "BridgeDestroyed",
        "BridgeMerged",
        "BridgeBlindTransfer",
        "BridgeAttendedTransfer",
        "PlaybackStarted",
        "PlaybackFinished",
        "RecordingStarted",
        "RecordingFinished",
        "RecordingFailed",
    };

    /// <summary>
    /// Gets the activity source of the Asterisk provider's spans.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(MeterName);

    /// <summary>
    /// Records that the real-time ingestion buffer for a provider filled and began applying backpressure. This is
    /// recorded once per saturation episode — on the first wait — not on every full write and not on a drop.
    /// </summary>
    /// <param name="providerName">The provider technical name whose ingestion buffer saturated, used as a low-cardinality metric dimension.</param>
    public static void RecordRealtimeIngestionSaturated(string providerName)
    {
        var provider = new KeyValuePair<string, object>("provider", providerName ?? "unspecified");

        _realtimeIngestionSaturated.Add(1, provider);
        _ingestionSaturated.Add(1, provider);
    }

    /// <summary>
    /// Records that the real-time voice listener successfully established its ARI event-stream connection. This is
    /// recorded once per successful connect, so the first connection and every reconnection each count once.
    /// </summary>
    /// <param name="providerName">The provider technical name that connected, used as a low-cardinality metric dimension.</param>
    public static void RecordRealtimeConnected(string providerName)
    {
        var provider = new KeyValuePair<string, object>("provider", providerName ?? "unspecified");

        _realtimeConnected.Add(1, provider);
        _connections.Add(1, provider);
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
        var provider = new KeyValuePair<string, object>("provider", providerName ?? "unspecified");

        _realtimeReconnectAttempted.Add(1, provider);
        _reconnectAttempts.Add(1, provider);
    }

    /// <summary>
    /// Starts following a connected real-time voice listener: it counts on <c>crestapps.asterisk.realtime.connected</c>
    /// and feeds <c>crestapps.asterisk.realtime.last_event_age</c> and <c>crestapps.asterisk.realtime.ingestion.queue_depth</c>
    /// until the returned tracking is disposed, which must happen when the connection ends.
    /// </summary>
    /// <param name="providerName">The provider technical name that connected.</param>
    /// <returns>The tracking of the connection.</returns>
    public static RealtimeListenerTracking TrackRealtimeListener(string providerName)
    {
        var tracking = new RealtimeListenerTracking(providerName ?? "unspecified");

        _listeners.TryAdd(tracking, 0);
        _connected.Add(1, new KeyValuePair<string, object>("provider", tracking.ProviderName));

        return tracking;
    }

    /// <summary>
    /// Records one ARI event the real-time listener received.
    /// </summary>
    /// <param name="providerName">The provider technical name.</param>
    /// <param name="eventType">The ARI event type, or <see langword="null"/> when the payload was not read.</param>
    /// <param name="result"><see cref="EventApplied"/>, <see cref="EventIgnored"/> or <see cref="EventFailed"/>.</param>
    public static void RecordRealtimeEvent(string providerName, string eventType, string result)
    {
        if (!_events.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, providerName ?? "unspecified" },
            { EventTypeTag, GetEventType(eventType) },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        _events.Add(1, tags);
    }

    /// <summary>
    /// Records how long after its own timestamp an ARI event was dispatched. A negative lag, from clocks that
    /// disagree, is recorded as zero.
    /// </summary>
    /// <param name="providerName">The provider technical name.</param>
    /// <param name="occurredUtc">The event's timestamp, or <see langword="null"/> when it had none.</param>
    /// <param name="nowUtc">The current time.</param>
    public static void RecordRealtimeEventLag(string providerName, DateTime? occurredUtc, DateTime nowUtc)
    {
        if (occurredUtc is not { } occurred || !_eventLag.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, providerName ?? "unspecified" },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        _eventLag.Record(Math.Max(0, (nowUtc - occurred).TotalSeconds), tags);
    }

    /// <summary>
    /// Starts measuring one ARI REST request: a client span and <c>crestapps.asterisk.ari.request.duration</c>.
    /// Finish it with <see cref="CompleteAriRequest(TelemetryScope, HttpStatusCode?, Exception)"/>.
    /// </summary>
    /// <param name="providerName">The provider technical name.</param>
    /// <param name="operation">The ARI client operation, such as <c>OriginateAsync</c>.</param>
    /// <returns>The scope of the request.</returns>
    public static TelemetryScope StartAriRequest(string providerName, string operation)
    {
        return TelemetryScope.Start(
            ActivitySource,
            "crestapps.asterisk.ari.request",
            _ariRequestDuration,
            new TagList
            {
                { CrestAppsOrchardCoreTelemetry.ProviderTag, providerName ?? "unspecified" },
                { CrestAppsOrchardCoreTelemetry.OperationTag, operation ?? CrestAppsOrchardCoreTelemetry.Unknown },
            },
            ActivityKind.Client);
    }

    /// <summary>
    /// Records how an ARI request ended: the <c>result</c> (<c>ok</c>, <c>timeout</c>, <see cref="AriResultUnreachable"/>,
    /// <see cref="AriResultNotFound"/>, <see cref="AriResultHttp4xx"/> or <see cref="AriResultHttp5xx"/>) and the outcome.
    /// </summary>
    /// <param name="scope">The scope started with <see cref="StartAriRequest(string, string)"/>.</param>
    /// <param name="statusCode">The status code of the response, or <see langword="null"/> when none arrived.</param>
    /// <param name="exception">The exception the request failed with, or <see langword="null"/>.</param>
    /// <param name="notFoundIsAnAnswer">Whether a 404 answers the request rather than failing it, such as asking
    /// whether a channel still exists or whether a recording is ready.</param>
    public static void CompleteAriRequest(TelemetryScope scope, HttpStatusCode? statusCode, Exception exception = null, bool notFoundIsAnAnswer = false)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var result = notFoundIsAnAnswer && statusCode == HttpStatusCode.NotFound
            ? AriResultNotFound
            : GetAriResult(statusCode, exception);

        scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.ResultTag, result);

        if (statusCode is { } code)
        {
            scope.SetSpanTag("http.response.status_code", (int)code);
        }

        if (exception is not null)
        {
            scope.Fail(exception);

            // A timeout of the resilience pipeline is not a TimeoutException, but it is one.
            if (result == CrestAppsOrchardCoreTelemetry.OutcomeTimeout)
            {
                scope.SetOutcome(CrestAppsOrchardCoreTelemetry.OutcomeTimeout);
            }
        }
        else if (result is CrestAppsOrchardCoreTelemetry.OutcomeOk or AriResultNotFound)
        {
            scope.Succeed();
        }
        else
        {
            scope.Fail(result);
        }
    }

    /// <summary>
    /// Gets the bounded <c>result</c> of an ARI request.
    /// </summary>
    /// <param name="statusCode">The status code of the response, or <see langword="null"/> when none arrived.</param>
    /// <param name="exception">The exception the request failed with, or <see langword="null"/>.</param>
    /// <returns>The result.</returns>
    public static string GetAriResult(HttpStatusCode? statusCode, Exception exception = null)
    {
        if (statusCode is { } code)
        {
            return (int)code switch
            {
                >= 500 => AriResultHttp5xx,
                >= 400 => AriResultHttp4xx,
                _ => CrestAppsOrchardCoreTelemetry.OutcomeOk,
            };
        }

        if (exception is TimeoutRejectedException)
        {
            return CrestAppsOrchardCoreTelemetry.OutcomeTimeout;
        }

        return CrestAppsOrchardCoreTelemetry.GetOutcome(exception ?? new InvalidOperationException()) switch
        {
            CrestAppsOrchardCoreTelemetry.OutcomeTimeout => CrestAppsOrchardCoreTelemetry.OutcomeTimeout,
            CrestAppsOrchardCoreTelemetry.OutcomeCancelled => CrestAppsOrchardCoreTelemetry.OutcomeCancelled,
            _ => AriResultUnreachable,
        };
    }

    /// <summary>
    /// Gets the bounded <see cref="EventTypeTag"/> of an ARI event type.
    /// </summary>
    /// <param name="eventType">The ARI event type.</param>
    /// <returns>The event type when it is one of <see cref="KnownEventTypes"/>, <c>unknown</c> when there is none,
    /// otherwise <c>other</c>.</returns>
    public static string GetEventType(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return CrestAppsOrchardCoreTelemetry.Unknown;
        }

        return _knownEventTypes.TryGetValue(eventType.Trim(), out var known)
            ? known
            : CrestAppsOrchardCoreTelemetry.Other;
    }

    private static IEnumerable<Measurement<double>> ObserveLastEventAge()
    {
        return _listeners.Keys
            .GroupBy(listener => listener.ProviderName, StringComparer.Ordinal)
            .Select(group => new Measurement<double>(
                group.Max(listener => listener.SinceLastEvent.TotalSeconds),
                new KeyValuePair<string, object>(CrestAppsOrchardCoreTelemetry.ProviderTag, group.Key)));
    }

    private static IEnumerable<Measurement<long>> ObserveQueueDepth()
    {
        return _listeners.Keys
            .GroupBy(listener => listener.ProviderName, StringComparer.Ordinal)
            .Select(group => new Measurement<long>(
                group.Sum(listener => (long)listener.QueueDepth),
                new KeyValuePair<string, object>(CrestAppsOrchardCoreTelemetry.ProviderTag, group.Key)));
    }

    /// <summary>
    /// One connected real-time voice listener, followed for the connection gauges until it is disposed.
    /// </summary>
    public sealed class RealtimeListenerTracking : IDisposable
    {
        private long _lastEventTimestamp = Stopwatch.GetTimestamp();
        private Func<int> _queueDepth;
        private int _disposed;

        internal RealtimeListenerTracking(string providerName)
        {
            ProviderName = providerName;
        }

        /// <summary>
        /// Gets the provider technical name of the listener.
        /// </summary>
        public string ProviderName { get; }

        /// <summary>
        /// Gets the time since the listener connected or last received an event.
        /// </summary>
        public TimeSpan SinceLastEvent => Stopwatch.GetElapsedTime(Interlocked.Read(ref _lastEventTimestamp));

        /// <summary>
        /// Gets the number of events buffered for dispatch.
        /// </summary>
        public int QueueDepth => Volatile.Read(ref _queueDepth)?.Invoke() ?? 0;

        /// <summary>
        /// Sets how the number of buffered events is read.
        /// </summary>
        /// <param name="queueDepth">Reads the number of buffered events.</param>
        public void ObserveQueue(Func<int> queueDepth)
            => Volatile.Write(ref _queueDepth, queueDepth);

        /// <summary>
        /// Records that the listener received an event.
        /// </summary>
        public void EventReceived()
            => Interlocked.Exchange(ref _lastEventTimestamp, Stopwatch.GetTimestamp());

        /// <summary>
        /// Stops following the listener, which is no longer connected.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _listeners.TryRemove(this, out _);
            _connected.Add(-1, new KeyValuePair<string, object>("provider", ProviderName));
        }
    }
}
