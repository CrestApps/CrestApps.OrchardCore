using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Webhooks.Core.Telemetry;

/// <summary>
/// The OpenTelemetry instruments of webhooks, on the meter and activity source <c>CrestApps.OrchardCore.Webhooks</c>:
/// outbound deliveries to subscribers (<c>crestapps.webhook.deliveries</c>, their send duration and the pending
/// backlog), and the provider webhook inbox that durably accepts and dispatches inbound provider callbacks
/// (<c>crestapps.webhook.inbox.*</c>). No metric carries a URL, a host, a subscription or a delivery id.
/// </summary>
public static class WebhookTelemetry
{
    /// <summary>
    /// The area of the meter and activity source, which are named <c>CrestApps.OrchardCore.Webhooks</c>.
    /// </summary>
    public const string Area = "Webhooks";

    /// <summary>
    /// The value of <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/> on the send of an outbound delivery.
    /// </summary>
    public const string OutboundProvider = "webhook";

    /// <summary>
    /// The delivery reached its subscriber.
    /// </summary>
    public const string DeliveryOk = "ok";

    /// <summary>
    /// The attempt failed and the delivery is tried again later.
    /// </summary>
    public const string DeliveryRetry = "retry";

    /// <summary>
    /// The delivery failed for good.
    /// </summary>
    public const string DeliveryError = "error";

    internal const string HandlerTag = "handler";

    internal const string DeliveryTag = "delivery";

    internal const string LimitTag = "limit";

    internal static readonly Meter Meter = CrestAppsOrchardCoreTelemetry.CreateMeter(Area);

    internal static readonly ActivitySource ActivitySource = CrestAppsOrchardCoreTelemetry.CreateActivitySource(Area);

    internal static readonly Counter<long> Deliveries = Meter.CreateCounter<long>(
        "crestapps.webhook.deliveries",
        "{attempt}",
        "The number of outbound webhook delivery attempts, by outcome (ok, retry or error), reason and response status class.");

    internal static readonly Histogram<double> DeliveryDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.webhook.delivery.duration",
        "The time an outbound webhook delivery attempt took to send, by outcome and response status class.");

    internal static readonly Gauge<long> DeliveriesPending = Meter.CreateGauge<long>(
        "crestapps.webhook.deliveries.pending",
        "{delivery}",
        "The number of outbound webhook deliveries waiting for their next attempt, as the delivery task last counted them.");

    internal static readonly Counter<long> InboxAccepted = Meter.CreateCounter<long>(
        "crestapps.webhook.inbox.accepted",
        "{delivery}",
        "The number of provider webhook deliveries offered to the inbox, by provider and result: accepted, duplicate or busy.");

    internal static readonly Counter<long> InboxDispatches = Meter.CreateCounter<long>(
        "crestapps.webhook.inbox.dispatches",
        "{dispatch}",
        "The number of provider webhook inbox dispatches, first attempt or retry, by result.");

    internal static readonly Histogram<double> InboxHandlerDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.webhook.inbox.handler.duration",
        "The time a provider webhook inbox handler took to handle one delivery, by handler and outcome.");

    internal static readonly Histogram<double> InboxLag = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.webhook.inbox.lag",
        "The time from when a provider webhook delivery was accepted to when a dispatch of it started.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    internal static readonly Counter<long> InboxDeadLettered = Meter.CreateCounter<long>(
        "crestapps.webhook.inbox.dead_lettered",
        "{delivery}",
        "The number of provider webhook deliveries dead-lettered after exhausting their retries, by provider and handler.");

    internal static readonly Gauge<long> InboxPendingAge = Meter.CreateGauge<long>(
        "crestapps.webhook.inbox.pending.age",
        "s",
        "The age of the oldest provider webhook delivery that was due when the last inbox pass ran.");

    internal static readonly Counter<long> IngressLimited = Meter.CreateCounter<long>(
        "crestapps.webhook.ingress.limited",
        "{request}",
        "The number of provider webhook requests refused by the ingress limiter, by limit: concurrency or rate.");

    /// <summary>
    /// Records what one outbound delivery attempt came to.
    /// </summary>
    /// <param name="outcome"><see cref="DeliveryOk"/>, <see cref="DeliveryRetry"/> or <see cref="DeliveryError"/>.</param>
    /// <param name="reason">Why, from a bounded set: <c>none</c>, <c>deleted</c>, <c>disabled</c>, <c>no_secret</c>,
    /// <c>egress_refused</c>, <c>exhausted</c>, <c>status</c>, <c>timeout</c> or <c>network</c>.</param>
    /// <param name="statusCode">The response status code, or <see langword="null"/> when nothing answered.</param>
    internal static void RecordDelivery(string outcome, string reason, int? statusCode)
    {
        if (!Deliveries.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
            { CrestAppsOrchardCoreTelemetry.ReasonTag, reason },
            { CrestAppsOrchardCoreTelemetry.StatusClassTag, ExternalCallTelemetry.GetStatusClass(statusCode) },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        Deliveries.Add(1, tags);
    }

    /// <summary>
    /// Records the number of outbound deliveries waiting for their next attempt.
    /// </summary>
    /// <param name="pending">The number pending.</param>
    public static void RecordPendingDeliveries(long pending)
        => RecordGauge(DeliveriesPending, pending);

    /// <summary>
    /// Records what became of a provider webhook delivery offered to the inbox.
    /// </summary>
    /// <param name="provider">The canonical provider name.</param>
    /// <param name="result"><c>accepted</c>, <c>duplicate</c> or <c>busy</c>.</param>
    internal static void RecordInboxAccepted(string provider, string result)
    {
        Activity.Current?.SetTag("crestapps.webhook.inbox.result", result);

        if (!InboxAccepted.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, provider ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        InboxAccepted.Add(1, tags);
    }

    /// <summary>
    /// Records how one dispatch of an inbox delivery ended.
    /// </summary>
    /// <param name="activity">The span of the dispatch, or <see langword="null"/>.</param>
    /// <param name="delivery"><c>first</c> or <c>retry</c>.</param>
    /// <param name="result"><c>completed</c>, <c>retry</c>, <c>dead_lettered</c>, <c>handler_unavailable</c> or <c>conflict</c>.</param>
    internal static void RecordInboxDispatch(Activity activity, string delivery, string result)
    {
        activity?.SetTag(CrestAppsOrchardCoreTelemetry.ResultTag, result);

        if (!InboxDispatches.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { DeliveryTag, delivery },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        InboxDispatches.Add(1, tags);
    }

    /// <summary>
    /// Records how long an inbox handler took, and how it ended.
    /// </summary>
    /// <param name="handler">The handler's stable technical name, from the bounded set of registered handlers.</param>
    /// <param name="elapsed">The time it took.</param>
    /// <param name="exception">The exception it failed with, or <see langword="null"/>.</param>
    internal static void RecordInboxHandler(string handler, TimeSpan elapsed, Exception exception)
    {
        if (!InboxHandlerDuration.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { HandlerTag, handler ?? CrestAppsOrchardCoreTelemetry.Unknown },
            {
                CrestAppsOrchardCoreTelemetry.OutcomeTag,
                exception is null ? CrestAppsOrchardCoreTelemetry.OutcomeOk : CrestAppsOrchardCoreTelemetry.GetOutcome(exception)
            },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        InboxHandlerDuration.Record(elapsed.TotalSeconds, tags);
    }

    /// <summary>
    /// Records how long after its acceptance a dispatch of an inbox delivery started.
    /// </summary>
    /// <param name="delivery"><c>first</c> or <c>retry</c>.</param>
    /// <param name="lag">The time since it was accepted.</param>
    internal static void RecordInboxLag(string delivery, TimeSpan lag)
    {
        if (!InboxLag.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { DeliveryTag, delivery },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        InboxLag.Record(Math.Max(0, lag.TotalSeconds), tags);
    }

    /// <summary>
    /// Records that an inbox delivery was dead-lettered.
    /// </summary>
    /// <param name="provider">The canonical provider name.</param>
    /// <param name="handler">The handler's technical name.</param>
    internal static void RecordInboxDeadLettered(string provider, string handler)
    {
        if (!InboxDeadLettered.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, provider ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { HandlerTag, handler ?? CrestAppsOrchardCoreTelemetry.Unknown },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        InboxDeadLettered.Add(1, tags);
    }

    /// <summary>
    /// Records the age of the oldest inbox delivery a pass found due.
    /// </summary>
    /// <param name="age">The age, or <see cref="TimeSpan.Zero"/> when nothing was due.</param>
    internal static void RecordInboxPendingAge(TimeSpan age)
        => RecordGauge(InboxPendingAge, (long)Math.Max(0, age.TotalSeconds));

    /// <summary>
    /// Records that the ingress limiter refused a provider webhook request.
    /// </summary>
    /// <param name="limit"><c>concurrency</c> or <c>rate</c>.</param>
    internal static void RecordIngressLimited(string limit)
    {
        if (!IngressLimited.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { LimitTag, limit },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        IngressLimited.Add(1, tags);
    }

    private static void RecordGauge(Gauge<long> gauge, long value)
    {
        if (!gauge.Enabled)
        {
            return;
        }

        var tags = new TagList();

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        gauge.Record(value, tags);
    }
}
