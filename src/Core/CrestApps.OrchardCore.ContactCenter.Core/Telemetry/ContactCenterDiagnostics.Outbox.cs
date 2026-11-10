using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// The durable event outbox: how each domain event reaches its handlers, first time or on redelivery, and how long
/// each handler takes.
/// </summary>
/// <remarks>
/// This measures the delivery mechanism, not the domain: an event delivered twice is two deliveries. The domain counts
/// (calls, offers, queues) are recorded where the state changes, so a redelivery never counts a call twice.
/// </remarks>
public static partial class ContactCenterDiagnostics
{
    /// <summary>
    /// The name of the span of one dispatch of an outbox message to its handlers. It links to the trace that published
    /// the event.
    /// </summary>
    internal const string OutboxDispatchSpanName = "crestapps.contactcenter.outbox.dispatch";

    /// <summary>
    /// The first dispatch of an event, right after the commit that published it.
    /// </summary>
    internal const string DeliveryFirst = "first";

    /// <summary>
    /// A redelivery of an event by the outbox's retry pass.
    /// </summary>
    internal const string DeliveryRetry = "retry";

    private static readonly Counter<long> _outboxRedeliveredNew = Meter.CreateCounter<long>(
        "crestapps.contactcenter.outbox.redelivered",
        "{message}",
        "The number of Contact Center domain events successfully redelivered from the durable outbox.");

    private static readonly Counter<long> _outboxDeadLetteredNew = Meter.CreateCounter<long>(
        "crestapps.contactcenter.outbox.dead_lettered",
        "{message}",
        "The number of Contact Center domain events dead-lettered after exhausting their retry budget, by reason.");

    private static readonly Counter<long> _outboxDispatches = Meter.CreateCounter<long>(
        "crestapps.contactcenter.outbox.dispatches",
        "{dispatch}",
        "The number of outbox dispatches, first time or redelivery, by result: completed or retry.");

    private static readonly Histogram<double> _outboxLag = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.outbox.lag",
        "The time from when a domain event was recorded to when a dispatch of it started, first time or redelivery.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    private static readonly Histogram<double> _outboxHandlerDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.outbox.handler.duration",
        "The time one outbox handler took to handle one domain event, by handler and result.");

    /// <summary>
    /// Starts the span of one dispatch of an outbox message, linked to the trace that published its event.
    /// </summary>
    /// <param name="eventType">The event type, a constant of the Contact Center.</param>
    /// <param name="delivery"><see cref="DeliveryFirst"/> or <see cref="DeliveryRetry"/>.</param>
    /// <param name="traceParent">The <c>traceparent</c> stored with the event, or <see langword="null"/>.</param>
    /// <returns>The span, or <see langword="null"/> when nothing listens.</returns>
    internal static Activity StartOutboxDispatch(string eventType, string delivery, string traceParent)
    {
        var activity = TraceContextPropagation.StartLinkedActivity(ActivitySource, OutboxDispatchSpanName, traceParent);

        if (activity is not null)
        {
            activity.SetTag("crestapps.contactcenter.event.type", eventType);
            activity.SetTag(DeliveryTag, delivery);
        }

        return activity;
    }

    /// <summary>
    /// Records that a dispatch of an outbox message started, and how long after its event was recorded.
    /// </summary>
    /// <param name="delivery"><see cref="DeliveryFirst"/> or <see cref="DeliveryRetry"/>.</param>
    /// <param name="lag">The time since the event was recorded.</param>
    internal static void RecordOutboxLag(string delivery, TimeSpan lag)
    {
        if (!_outboxLag.Enabled)
        {
            return;
        }

        Record(_outboxLag, Math.Max(0, lag.TotalSeconds), new TagList
        {
            { DeliveryTag, delivery },
        });
    }

    /// <summary>
    /// Records how a dispatch of an outbox message ended.
    /// </summary>
    /// <param name="activity">The span of the dispatch, or <see langword="null"/>.</param>
    /// <param name="delivery"><see cref="DeliveryFirst"/> or <see cref="DeliveryRetry"/>.</param>
    /// <param name="completed">Whether every handler completed, so the message was removed.</param>
    internal static void RecordOutboxDispatch(Activity activity, string delivery, bool completed)
    {
        var result = completed ? "completed" : "retry";

        activity?.SetTag(CrestAppsOrchardCoreTelemetry.ResultTag, result);

        if (!completed)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "A handler failed; the message is scheduled for retry.");
        }

        if (!_outboxDispatches.Enabled)
        {
            return;
        }

        Add(_outboxDispatches, new TagList
        {
            { DeliveryTag, delivery },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        });
    }

    /// <summary>
    /// Records how long one handler took to handle one event, and how it ended.
    /// </summary>
    /// <param name="handlerId">The handler's stable id, from the bounded set of registered handlers.</param>
    /// <param name="elapsed">The time it took.</param>
    /// <param name="exception">The exception it failed with, or <see langword="null"/>.</param>
    internal static void RecordOutboxHandler(string handlerId, TimeSpan elapsed, Exception exception)
    {
        if (!_outboxHandlerDuration.Enabled)
        {
            return;
        }

        Record(_outboxHandlerDuration, elapsed.TotalSeconds, new TagList
        {
            { HandlerTag, handlerId ?? CrestAppsOrchardCoreTelemetry.Unknown },
            {
                CrestAppsOrchardCoreTelemetry.ResultTag,
                exception is null ? CrestAppsOrchardCoreTelemetry.OutcomeOk : CrestAppsOrchardCoreTelemetry.GetOutcome(exception)
            },
        });
    }
}
