using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telnyx.Services;

namespace CrestApps.OrchardCore.Telnyx.Telemetry;

/// <summary>
/// The OpenTelemetry spans and metrics of the Telnyx provider: its REST API, its call and messaging webhooks, the
/// voicemail fast path and the media stream. The meter and activity source are both named
/// <see cref="SourceName"/>. Nothing is recorded, and next to nothing is spent, while no listener is attached.
/// </summary>
/// <remarks>
/// Metrics carry bounded tags only. The call-control id, phone numbers and SIP URIs never go on a metric, and the
/// API spans carry the status code but never the request path, which holds the call-control id.
/// </remarks>
public static class TelnyxTelemetry
{
    /// <summary>
    /// The name of the meter and of the activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Telnyx";

    /// <summary>
    /// The value of the <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/>.
    /// </summary>
    public const string ProviderName = TelnyxConstants.ProviderTechnicalName;

    /// <summary>
    /// The tag that records whether a REST request was retried before it ended.
    /// </summary>
    public const string RetriedTag = "retried";

    /// <summary>
    /// The tag that records the type of a webhook event, such as <c>call.answered</c>, or <c>other</c> for a type
    /// that is not in <see cref="KnownWebhookTypes"/>.
    /// </summary>
    public const string WebhookTypeTag = "type";

    /// <summary>
    /// The tag that records which webhook a delivery reached: <see cref="WebhookKindCall"/> or <see cref="WebhookKindSms"/>.
    /// </summary>
    public const string WebhookKindTag = "kind";

    /// <summary>
    /// The call control webhook.
    /// </summary>
    public const string WebhookKindCall = "call";

    /// <summary>
    /// The messaging webhook.
    /// </summary>
    public const string WebhookKindSms = "sms";

    /// <summary>
    /// The delivery was stored or processed.
    /// </summary>
    public const string WebhookAccepted = "accepted";

    /// <summary>
    /// The delivery was a redelivery of one already accepted.
    /// </summary>
    public const string WebhookDuplicate = "duplicate";

    /// <summary>
    /// The delivery's signature could not be validated, or no key is configured to validate it with.
    /// </summary>
    public const string WebhookRejectedSignature = "rejected_signature";

    /// <summary>
    /// The delivery's signed timestamp was too old or too far in the future.
    /// </summary>
    public const string WebhookRejectedStale = "rejected_stale";

    /// <summary>
    /// The delivery was refused by the ingress concurrency or rate limit, or the inbox was too busy to store it.
    /// </summary>
    public const string WebhookRejectedRate = "rejected_rate";

    /// <summary>
    /// The delivery was larger than the endpoint accepts.
    /// </summary>
    public const string WebhookRejectedSize = "rejected_size";

    /// <summary>
    /// The provider is disabled on the tenant.
    /// </summary>
    public const string WebhookDisabled = "disabled";

    /// <summary>
    /// The delivery was signed but could not be parsed.
    /// </summary>
    public const string WebhookParseError = "parse_error";

    /// <summary>
    /// The delivery could not be stored, and the provider is asked to send it again.
    /// </summary>
    public const string WebhookStoreFailed = "store_failed";

    /// <summary>
    /// A broken media stream was dialled back in, or started again, and the call carried on over it.
    /// </summary>
    public const string MediaStreamReconnected = "reconnected";

    /// <summary>
    /// A broken media stream did not come back in time, and the call was handed on.
    /// </summary>
    public const string MediaStreamLost = "lost";

    /// <summary>
    /// The webhook event types recorded as themselves on the <see cref="WebhookTypeTag"/>; any other type is
    /// recorded as <c>other</c>.
    /// </summary>
    public static IReadOnlySet<string> KnownWebhookTypes { get; } = new string[]
    {
        "call.initiated",
        "call.answered",
        "call.bridged",
        "call.hangup",
        "call.ringing",
        "call.dtmf.received",
        "call.gather.ended",
        "call.machine.detection.ended",
        "call.machine.greeting.ended",
        "call.machine.premium.detection.ended",
        "call.machine.premium.greeting.ended",
        "call.playback.started",
        "call.playback.ended",
        "call.speak.started",
        "call.speak.ended",
        "call.recording.saved",
        "call.recording.error",
        "call.transcription",
        "call.fork.started",
        "call.fork.stopped",
        "call.refer.started",
        "call.refer.completed",
        "call.refer.failed",
        "call.cost",
        "conference.created",
        "conference.ended",
        "conference.participant.joined",
        "conference.participant.left",
        "streaming.started",
        "streaming.stopped",
        "streaming.failed",
        "message.received",
        "message.sent",
        "message.finalized",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> _apiResources = new(StringComparer.OrdinalIgnoreCase)
    {
        ["calls"] = "calls",
        ["conferences"] = "conferences",
        ["telephony_credentials"] = "credentials",
        ["available_phone_numbers"] = "available_numbers",
        ["number_orders"] = "number_orders",
        ["phone_numbers"] = "phone_numbers",
    };

    /// <summary>
    /// Gets the meter of the Telnyx provider.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Telnyx");

    /// <summary>
    /// Gets the activity source of the Telnyx provider.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Telnyx");

    /// <summary>
    /// The duration of a Telnyx REST request, retries included, tagged with the <c>provider</c>, the
    /// <c>operation</c> (see <see cref="GetApiOperation(HttpMethod, string)"/>), the <c>status_class</c>,
    /// <see cref="RetriedTag"/> and the <c>outcome</c>. Its count is the number of requests.
    /// </summary>
    public static Histogram<double> ApiRequestDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.telnyx.api.request.duration",
        "The duration of Telnyx REST API requests, retries included.");

    /// <summary>
    /// The number of webhook deliveries received, tagged with <see cref="WebhookKindTag"/>, <see cref="WebhookTypeTag"/>
    /// and the <c>result</c>.
    /// </summary>
    public static Counter<long> Webhooks { get; } = Meter.CreateCounter<long>(
        "crestapps.telnyx.webhooks",
        "{delivery}",
        "The number of Telnyx webhook deliveries received, by what became of them.");

    /// <summary>
    /// The time from the event a webhook reports to its arrival, tagged with <see cref="WebhookKindTag"/> and
    /// <see cref="WebhookTypeTag"/>.
    /// </summary>
    public static Histogram<double> WebhookLag { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.telnyx.webhook.lag",
        "The time from the event a Telnyx webhook reports to the delivery arriving.");

    /// <summary>
    /// The duration of processing a call webhook delivery, inline or from the webhook inbox, tagged with
    /// <see cref="WebhookTypeTag"/> and the <c>outcome</c>.
    /// </summary>
    public static Histogram<double> WebhookProcessDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.telnyx.webhook.process.duration",
        "The duration of processing a Telnyx call webhook delivery.");

    /// <summary>
    /// The time from a voicemail greeting ending to the recording being started, on the webhook's fast path, tagged
    /// with the <c>outcome</c>.
    /// </summary>
    public static Histogram<double> VoicemailRecordStartLatency { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.telnyx.voicemail.record_start.latency",
        "The time from receiving the greeting-ended webhook to the voicemail recording being started.");

    /// <summary>
    /// The number of times Telnyx reported a call's media stream failed.
    /// </summary>
    public static Counter<long> MediaStreamFailures { get; } = Meter.CreateCounter<long>(
        "crestapps.telnyx.media.stream.failures",
        "{failure}",
        "The number of times Telnyx reported a call's media stream failed.");

    /// <summary>
    /// The number of broken media streams, tagged with the <c>result</c>: <see cref="MediaStreamReconnected"/> or
    /// <see cref="MediaStreamLost"/>.
    /// </summary>
    public static Counter<long> MediaStreamReconnects { get; } = Meter.CreateCounter<long>(
        "crestapps.telnyx.media.stream.reconnects",
        "{reconnect}",
        "The number of broken Telnyx media streams, by whether they came back.");

    /// <summary>
    /// The time from a media stream breaking to the replacement socket taking over.
    /// </summary>
    public static Histogram<double> MediaStreamReconnectDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.telnyx.media.stream.reconnect.duration",
        "The time from a Telnyx media stream breaking to the replacement socket taking over.");

    /// <summary>
    /// Gets the bounded <c>operation</c> of a REST request from its method and path: the resource and the action, such
    /// as <c>calls.answer</c>, <c>calls.create</c> or <c>conferences.join</c>. The ids in the path are dropped, and a
    /// resource that is not known is <c>other</c>.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The request path, relative to the API base address, with any query string.</param>
    /// <returns>The operation.</returns>
    public static string GetApiOperation(HttpMethod method, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return CrestAppsOrchardCoreTelemetry.Other;
        }

        var queryStart = path.IndexOf('?', StringComparison.Ordinal);
        var segments = (queryStart >= 0 ? path[..queryStart] : path)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0 || !_apiResources.TryGetValue(segments[0], out var resource))
        {
            return CrestAppsOrchardCoreTelemetry.Other;
        }

        var verb = method?.Method?.ToUpperInvariant();

        return segments.Length switch
        {
            1 => resource + "." + (verb switch
            {
                "POST" => "create",
                "GET" => "list",
                _ => CrestAppsOrchardCoreTelemetry.Other,
            }),
            2 => resource + "." + (verb switch
            {
                "GET" => "get",
                "DELETE" => "delete",
                "PATCH" or "PUT" => "update",
                _ => CrestAppsOrchardCoreTelemetry.Other,
            }),
            3 => resource + "." + AsName(segments[2]),
            4 when string.Equals(segments[2], "actions", StringComparison.OrdinalIgnoreCase) => resource + "." + AsName(segments[3]),
            _ => resource + "." + CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// Gets the bounded <see cref="WebhookTypeTag"/> of a webhook event type.
    /// </summary>
    /// <param name="eventType">The event type Telnyx sent.</param>
    /// <returns>The event type in lower case when it is one of <see cref="KnownWebhookTypes"/>; otherwise <c>other</c>.</returns>
    public static string GetWebhookType(string eventType)
    {
        var trimmed = eventType?.Trim();

        return !string.IsNullOrEmpty(trimmed) && KnownWebhookTypes.Contains(trimmed)
            ? trimmed.ToLowerInvariant()
            : CrestAppsOrchardCoreTelemetry.Other;
    }

    /// <summary>
    /// Records one webhook delivery.
    /// </summary>
    /// <param name="kind">The webhook: <see cref="WebhookKindCall"/> or <see cref="WebhookKindSms"/>.</param>
    /// <param name="type">The bounded event type, or <see langword="null"/> when it was not read.</param>
    /// <param name="result">What became of the delivery, such as <see cref="WebhookAccepted"/>.</param>
    public static void RecordWebhook(string kind, string type, string result)
    {
        if (!Webhooks.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { WebhookKindTag, kind },
            { WebhookTypeTag, type ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Webhooks.Add(1, tags);
    }

    /// <summary>
    /// Records how long after the event it reports a webhook arrived. A negative lag, from clocks that disagree, is
    /// recorded as zero.
    /// </summary>
    /// <param name="kind">The webhook.</param>
    /// <param name="type">The bounded event type.</param>
    /// <param name="occurredUtc">When the event happened, or <see langword="null"/> when the delivery does not say.</param>
    /// <param name="receivedUtc">When the delivery arrived.</param>
    public static void RecordWebhookLag(string kind, string type, DateTime? occurredUtc, DateTime receivedUtc)
    {
        if (occurredUtc is not { } occurred || !WebhookLag.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { WebhookKindTag, kind },
            { WebhookTypeTag, type ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        WebhookLag.Record(Math.Max(0, (receivedUtc - occurred).TotalSeconds), tags);
    }

    /// <summary>
    /// Processes a call webhook delivery in a <c>crestapps.telnyx.webhook.process</c> span linked to the span that
    /// received it, and records <see cref="WebhookProcessDuration"/>. The delivery's <see cref="TelnyxCallEvent.TraceParent"/>
    /// carries the receiving span across the webhook inbox.
    /// </summary>
    /// <param name="webhookService">The service that processes the event.</param>
    /// <param name="callEvent">The event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How the event was handled.</returns>
    public static async Task<TelnyxWebhookResult> ProcessWebhookAsync(
        ITelnyxWebhookService webhookService,
        TelnyxCallEvent callEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(webhookService);
        ArgumentNullException.ThrowIfNull(callEvent);

        using var telemetry = TelemetryScope.StartLinked(
            ActivitySource,
            "crestapps.telnyx.webhook.process",
            WebhookProcessDuration,
            callEvent.TraceParent,
            new TagList
            {
                { WebhookTypeTag, GetWebhookType(callEvent.EventType) },
            });

        telemetry.SetSpanTag("crestapps.telnyx.event.id", callEvent.EventId);

        try
        {
            var result = await webhookService.ProcessAsync(callEvent, cancellationToken);

            telemetry.AddMetricTag(CrestAppsOrchardCoreTelemetry.ResultTag, result switch
            {
                TelnyxWebhookResult.Updated => "updated",
                TelnyxWebhookResult.Routed => "routed",
                _ => "ignored",
            });
            telemetry.Succeed();

            return result;
        }
        catch (Exception ex)
        {
            telemetry.Fail(ex);

            throw;
        }
    }

    /// <summary>
    /// Records that Telnyx reported a media stream failed.
    /// </summary>
    public static void RecordMediaStreamFailure()
    {
        if (!MediaStreamFailures.Enabled)
        {
            return;
        }

        var tags = new TagList();
        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        MediaStreamFailures.Add(1, tags);
    }

    /// <summary>
    /// Records how a broken media stream ended up.
    /// </summary>
    /// <param name="result"><see cref="MediaStreamReconnected"/> or <see cref="MediaStreamLost"/>.</param>
    /// <param name="brokenFor">How long the stream was broken before it came back, or <see langword="null"/> when it did not.</param>
    public static void RecordMediaStreamReconnect(string result, TimeSpan? brokenFor)
    {
        var tags = new TagList();
        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);

        if (MediaStreamReconnects.Enabled)
        {
            var countTags = tags;
            countTags.Add(CrestAppsOrchardCoreTelemetry.ResultTag, result);
            MediaStreamReconnects.Add(1, countTags);
        }

        if (brokenFor is { } duration && MediaStreamReconnectDuration.Enabled)
        {
            MediaStreamReconnectDuration.Record(duration.TotalSeconds, tags);
        }
    }

    private static string AsName(string segment)
    {
        if (string.IsNullOrEmpty(segment) || segment.Length > 40)
        {
            return CrestAppsOrchardCoreTelemetry.Other;
        }

        foreach (var character in segment)
        {
            if (character is not ((>= 'a' and <= 'z') or '_'))
            {
                return CrestAppsOrchardCoreTelemetry.Other;
            }
        }

        return segment;
    }
}
