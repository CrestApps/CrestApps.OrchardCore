using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry;

/// <summary>
/// The OpenTelemetry instruments and spans of omnichannel messaging and SMS, under the meter and activity source
/// <c>CrestApps.OrchardCore.Messaging</c>: sends and the outbox that retries them, delivery receipts, inbound messages,
/// routing and assignment, the first-response SLA, automated (AI) SMS turns, re-engagement, broadcasts and activity
/// starts.
/// </summary>
/// <remarks>
/// Metrics carry only bounded tags: the channel, the provider, the routing trigger and router, and the result enums
/// documented on each instrument. Phone numbers, conversation, activity and message ids, and message bodies never go
/// on a metric; ids go on spans and logs only, and bodies nowhere.
/// </remarks>
public static partial class MessagingTelemetry
{
    /// <summary>
    /// The name of the meter and the activity source.
    /// </summary>
    public const string SourceName = CrestAppsOrchardCoreTelemetry.SourceNamePrefix + ".Messaging";

    /// <summary>
    /// The tag that records which router claimed a conversation.
    /// </summary>
    public const string RouterTag = "router";

    /// <summary>
    /// The tag that records a status: the assignment status of a conversation, or the delivery status of a message.
    /// </summary>
    public const string StatusTag = "status";

    /// <summary>
    /// The tag that records whether a first response met its target: <c>met</c>, <c>breached</c> or <c>none</c>.
    /// </summary>
    public const string SlaTag = "sla";

    /// <summary>
    /// The tag that records whether an automated agent decided to reply: <c>respond</c>, <c>skip</c> or <c>fallback</c>.
    /// </summary>
    public const string DecisionTag = "decision";

    /// <summary>
    /// The tag that records where an opt-out came from: <c>keyword</c>, <c>provider_refusal</c> or <c>automated_conversation</c>.
    /// </summary>
    public const string SourceTag = "source";

    /// <summary>
    /// The tag that records whether a contact opted out or back in: <c>opt_out</c> or <c>opt_in</c>.
    /// </summary>
    public const string ActionTag = "action";

    /// <summary>
    /// The span tag that records the result of a measured operation.
    /// </summary>
    public const string ResultSpanTag = "crestapps.messaging.result";

    /// <summary>
    /// The span tag that carries the id of a conversation. Spans only, never a metric.
    /// </summary>
    public const string ConversationIdSpanTag = "crestapps.messaging.conversation.id";

    /// <summary>
    /// The span tag that carries the id of an activity. Spans only, never a metric.
    /// </summary>
    public const string ActivityIdSpanTag = "crestapps.messaging.activity.id";

    /// <summary>
    /// The span tag that carries the id of the AI chat session an automated conversation is stored in. Spans only,
    /// never a metric.
    /// </summary>
    public const string AISessionIdSpanTag = "crestapps.messaging.ai.session.id";

    /// <summary>
    /// The span tag that carries the provider's id of a message. Spans only, never a metric.
    /// </summary>
    public const string ProviderMessageIdSpanTag = "crestapps.messaging.provider_message.id";

    /// <summary>
    /// The result of an operation that failed with an exception, or was never completed.
    /// </summary>
    public const string ResultError = "error";

    /// <summary>
    /// The result of an operation that was cancelled.
    /// </summary>
    public const string ResultCancelled = "cancelled";

    /// <summary>
    /// The value of the <see cref="CrestAppsOrchardCoreTelemetry.ChannelTag"/> for SMS.
    /// </summary>
    public const string ChannelSms = "sms";

    /// <summary>
    /// The meter of the messaging instruments.
    /// </summary>
    public static Meter Meter { get; } = CrestAppsOrchardCoreTelemetry.CreateMeter("Messaging");

    /// <summary>
    /// The activity source of the messaging spans.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = CrestAppsOrchardCoreTelemetry.CreateActivitySource("Messaging");

    /// <summary>
    /// The number of outbound messages handed to a provider, tagged with the channel, the <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/>
    /// and the result: <see cref="SendResults.Sent"/>, <see cref="SendResults.Failed"/>, <see cref="SendResults.OptedOut"/>,
    /// <see cref="SendResults.MediaNotSupported"/>, <see cref="SendResults.NoProvider"/> or <see cref="ResultError"/>.
    /// </summary>
    public static Counter<long> Send { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.send",
        "{message}",
        "The number of outbound messages handed to a provider, by result.");

    /// <summary>
    /// The time a provider took to accept or refuse an outbound message, tagged with the channel, the provider and the outcome.
    /// </summary>
    public static Histogram<double> SendDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.messaging.send.duration",
        "The time a provider took to accept or refuse an outbound message.");

    /// <summary>
    /// The number of times the outbox tried a queued outbound message again, tagged with the channel and the result:
    /// <see cref="OutboxResults.Sent"/>, <see cref="OutboxResults.Rescheduled"/> or <see cref="OutboxResults.Failed"/>.
    /// </summary>
    public static Counter<long> OutboxRetry { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.outbox.retry",
        "{attempt}",
        "The number of times the outbox tried a queued outbound message again, by result.");

    /// <summary>
    /// The number of outbound messages given up on, tagged with the channel and the <see cref="CrestAppsOrchardCoreTelemetry.ReasonTag"/>:
    /// <see cref="OutboxResults.RetriesExhausted"/> or <see cref="OutboxResults.NotRetryable"/>.
    /// </summary>
    public static Counter<long> OutboxFailed { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.outbox.failed",
        "{message}",
        "The number of outbound messages given up on and marked failed, by reason.");

    /// <summary>
    /// The number of opt-outs and opt-ins recorded, tagged with the channel, the <see cref="SourceTag"/> and the <see cref="ActionTag"/>.
    /// </summary>
    public static Counter<long> OptOut { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.optout",
        "{change}",
        "The number of channel opt-outs and opt-ins recorded, by source.");

    /// <summary>
    /// The number of provider delivery receipts, tagged with the channel, the delivery <see cref="StatusTag"/> and the
    /// result: <see cref="DeliveryResults.Applied"/> or <see cref="DeliveryResults.Unmatched"/>.
    /// </summary>
    public static Counter<long> Delivery { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.delivery",
        "{receipt}",
        "The number of provider delivery receipts, by delivery status and whether they matched a sent message.");

    /// <summary>
    /// The time from an outbound message being created to the receipt that settled it as delivered, failed or
    /// undelivered, tagged with the channel and the delivery <see cref="StatusTag"/>.
    /// </summary>
    public static Histogram<double> DeliveryLatency { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.messaging.delivery.latency",
        "The time from an outbound message being created to the receipt that settled its delivery.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// The number of inbound messages the workspace pipeline processed, tagged with the channel and the result (see
    /// <see cref="InboundResults"/>).
    /// </summary>
    public static Counter<long> Inbound { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.inbound",
        "{message}",
        "The number of inbound messages the messaging workspace processed, by result.");

    /// <summary>
    /// The time the workspace pipeline took to process an inbound message, tagged with the channel and the outcome.
    /// </summary>
    public static Histogram<double> InboundDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.messaging.inbound.duration",
        "The time the messaging workspace took to process an inbound message.");

    /// <summary>
    /// The time from an inbound message's <c>CreatedUtc</c> to the workspace recording it in a thread, tagged with the
    /// channel. <c>CreatedUtc</c> is the provider's receive time where the webhook carries one (Event Grid), otherwise
    /// the time the webhook arrived (Twilio, Telnyx). It grows when the provider webhook inbox backs up.
    /// </summary>
    public static Histogram<double> InboundLag { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.messaging.inbound.lag",
        "The time from an inbound message being received to the messaging workspace recording it.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// The number of automated (AI) conversation turns, tagged with the channel and the result (see <see cref="AITurnResults"/>).
    /// </summary>
    public static Counter<long> AITurn { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.ai.turn",
        "{turn}",
        "The number of automated conversation turns, by result.");

    /// <summary>
    /// The time an automated (AI) conversation turn took, from the customer's message to the reply leaving, including the
    /// humanized reading and typing pauses, tagged with the channel and the outcome.
    /// </summary>
    public static Histogram<double> AITurnDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.messaging.ai.turn.duration",
        "The time an automated conversation turn took, including the humanized pauses.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// The number of automated conversations handed off to a person, tagged with the channel and the result:
    /// <see cref="HandoffResults.HandedOff"/>, <see cref="HandoffResults.Failed"/> or <see cref="ResultError"/>.
    /// </summary>
    public static Counter<long> AIHandoff { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.ai.handoff",
        "{handoff}",
        "The number of automated conversations handed off to a person, by result.");

    /// <summary>
    /// The number of decisions an automated agent made about whether to reply, tagged with the channel and the
    /// <see cref="DecisionTag"/>: <see cref="RespondDecisions.Respond"/>, <see cref="RespondDecisions.Skip"/> or
    /// <see cref="RespondDecisions.Fallback"/>.
    /// </summary>
    public static Counter<long> AIRespondDecision { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.ai.respond_decision",
        "{decision}",
        "The number of decisions an automated agent made about whether to reply.");

    /// <summary>
    /// The number of routing passes, tagged with the channel, the <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/>
    /// and the <see cref="RouterTag"/> that claimed the conversation (<c>none</c> when none did).
    /// </summary>
    public static Counter<long> Route { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.route",
        "{routing}",
        "The number of conversation routing passes, by trigger and by the router that claimed the conversation.");

    /// <summary>
    /// The number of times a conversation's assignment changed, tagged with the channel, the
    /// <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/> and the assignment <see cref="StatusTag"/> it ended in.
    /// </summary>
    public static Counter<long> Assignment { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.assignment",
        "{assignment}",
        "The number of times a conversation's assignment changed, by trigger and resulting status.");

    /// <summary>
    /// The number of conversations that missed their first-response target, tagged with the channel. Each breach is
    /// counted once, when it is announced.
    /// </summary>
    public static Counter<long> SlaBreached { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.sla.breached",
        "{conversation}",
        "The number of conversations that missed their first-response target.");

    /// <summary>
    /// The number of routed conversations that were not picked up in time and were placed again, tagged with the channel
    /// and the result: <see cref="ReassignmentResults.Rerouted"/>, <see cref="ReassignmentResults.Pooled"/> or
    /// <see cref="ReassignmentResults.Unassigned"/>.
    /// </summary>
    public static Counter<long> Reassigned { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.reassigned",
        "{conversation}",
        "The number of routed conversations placed again because nobody picked them up in time.");

    /// <summary>
    /// The time from a conversation starting to the first reply a person sent on it, tagged with the channel and the
    /// <see cref="SlaTag"/>.
    /// </summary>
    public static Histogram<double> FirstResponseDuration { get; } = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.messaging.first_response.duration",
        "The time from a conversation starting to the first reply a person sent on it.",
        CrestAppsOrchardCoreTelemetry.LongDurationBucketBoundaries);

    /// <summary>
    /// The number of re-engagement nudges considered for automated conversations that went quiet, tagged with the channel
    /// and the result (see <see cref="ReEngagementResults"/>).
    /// </summary>
    public static Counter<long> ReEngagement { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.reengagement",
        "{attempt}",
        "The number of re-engagement nudges considered for quiet automated conversations, by result.");

    /// <summary>
    /// The number of automated conversations whose owed reply was re-driven after it was lost, tagged with the channel
    /// and the result: <see cref="OwedReplyResults.Redriven"/> or <see cref="ResultError"/>.
    /// </summary>
    public static Counter<long> OwedReplyRecovery { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.owed_reply.recovery",
        "{conversation}",
        "The number of automated conversations whose lost reply was re-driven, by result.");

    /// <summary>
    /// The number of broadcasts started and finished, tagged with the channel and the result (see <see cref="BroadcastResults"/>).
    /// </summary>
    public static Counter<long> Broadcast { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.broadcast",
        "{broadcast}",
        "The number of broadcasts started and finished, by result.");

    /// <summary>
    /// The number of broadcast recipients sent to, tagged with the channel and the result: <see cref="BroadcastResults.Sent"/>
    /// or <see cref="BroadcastResults.Failed"/>.
    /// </summary>
    public static Counter<long> BroadcastRecipient { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.broadcast.recipient",
        "{recipient}",
        "The number of broadcast recipients sent to, by result.");

    /// <summary>
    /// The number of automated conversations an activity starter decided on, tagged with the channel, the
    /// <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/> (<see cref="ActivityStartResults.TriggerEntryPoint"/> or
    /// <see cref="ActivityStartResults.TriggerAutomated"/>) and the result (see <see cref="ActivityStartResults"/>).
    /// </summary>
    public static Counter<long> ActivityStart { get; } = Meter.CreateCounter<long>(
        "crestapps.messaging.activity.start",
        "{activity}",
        "The number of automated conversations the activity starters decided on, by result.");

    /// <summary>
    /// Gets the <see cref="CrestAppsOrchardCoreTelemetry.ChannelTag"/> value of a channel: its technical name in lower
    /// case, such as <see cref="ChannelSms"/>. Pass only the name of an enabled channel, never a value read from a request.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <returns>The tag value, or <see cref="CrestAppsOrchardCoreTelemetry.Unknown"/>.</returns>
    public static string GetChannelTag(string channel)
    {
        if (string.IsNullOrEmpty(channel))
        {
            return CrestAppsOrchardCoreTelemetry.Unknown;
        }

        if (string.Equals(channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase))
        {
            return ChannelSms;
        }

        return channel.ToLowerInvariant();
    }

    /// <summary>
    /// Gets the <see cref="CrestAppsOrchardCoreTelemetry.ProviderTag"/> value of a provider: its registered technical name.
    /// </summary>
    /// <param name="provider">The technical name of the provider.</param>
    /// <returns>The tag value, or <see cref="CrestAppsOrchardCoreTelemetry.Unknown"/>.</returns>
    public static string GetProviderTag(string provider)
    {
        return string.IsNullOrEmpty(provider)
            ? CrestAppsOrchardCoreTelemetry.Unknown
            : provider;
    }

    private static MessagingMeasurement Start(
        string spanName,
        Histogram<double> duration,
        Counter<long> counter,
        string channel,
        ActivityKind kind)
    {
        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
        };

        return new MessagingMeasurement(TelemetryScope.Start(ActivitySource, spanName, duration, tags, kind), counter, tags);
    }

    private static void Add(Counter<long> counter, TagList tags)
    {
        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        counter.Add(1, tags);
    }
}
