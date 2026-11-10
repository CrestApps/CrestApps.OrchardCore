using System.Diagnostics;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry;

/// <summary>
/// The measurements of inbound messages, and of the delivery receipts providers send for outbound ones.
/// </summary>
public static partial class MessagingTelemetry
{
    /// <summary>
    /// Starts measuring an inbound message the workspace pipeline processes: the <c>crestapps.messaging.inbound</c> span,
    /// <see cref="InboundDuration"/>, tagged with the result.
    /// </summary>
    /// <param name="channel">The technical name of the channel, or <see langword="null"/> when no enabled channel serves it.</param>
    /// <returns>The measurement.</returns>
    public static MessagingMeasurement StartInbound(string channel)
    {
        return Start("crestapps.messaging.inbound", InboundDuration, channel, ActivityKind.Internal);
    }

    /// <summary>
    /// Records a provider delivery receipt and, when it settled the message, the time the delivery took.
    /// </summary>
    /// <param name="channel">The technical name of the channel, or <see langword="null"/> when no enabled channel serves it.</param>
    /// <param name="status">The delivery status the receipt reports.</param>
    /// <param name="result"><see cref="DeliveryResults.Applied"/> or <see cref="DeliveryResults.Unmatched"/>.</param>
    /// <param name="latency">The time from the message being created to this receipt, when the receipt moved the message
    /// to a final status; otherwise <see langword="null"/>.</param>
    public static void RecordDelivery(string channel, MessageDeliveryStatus status, string result, TimeSpan? latency = null)
    {
        if (!Delivery.Enabled && !DeliveryLatency.Enabled)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
            { StatusTag, GetDeliveryStatusTag(status) },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);

        if (latency is { } elapsed && elapsed >= TimeSpan.Zero)
        {
            DeliveryLatency.Record(elapsed.TotalSeconds, tags);
        }

        tags.Add(CrestAppsOrchardCoreTelemetry.ResultTag, result);
        Delivery.Add(1, tags);
    }

    /// <summary>
    /// Records the time from the provider receiving an inbound message to the workspace recording it.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <param name="lag">The lag.</param>
    public static void RecordInboundLag(string channel, TimeSpan lag)
    {
        if (!InboundLag.Enabled || lag < TimeSpan.Zero)
        {
            return;
        }

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        InboundLag.Record(lag.TotalSeconds, tags);
    }

    /// <summary>
    /// Gets the <see cref="StatusTag"/> value of a delivery status.
    /// </summary>
    /// <param name="status">The delivery status.</param>
    /// <returns><c>queued</c>, <c>sent</c>, <c>delivered</c>, <c>failed</c>, <c>undelivered</c> or <c>other</c>.</returns>
    public static string GetDeliveryStatusTag(MessageDeliveryStatus status)
    {
        return status switch
        {
            MessageDeliveryStatus.Queued => "queued",
            MessageDeliveryStatus.Sent => "sent",
            MessageDeliveryStatus.Delivered => "delivered",
            MessageDeliveryStatus.Failed => "failed",
            MessageDeliveryStatus.Undelivered => "undelivered",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// The results of <see cref="Delivery"/>.
    /// </summary>
    public static class DeliveryResults
    {
        /// <summary>
        /// The receipt matched a sent message and updated it.
        /// </summary>
        public const string Applied = "applied";

        /// <summary>
        /// The receipt matched no conversation or no sent message, and was ignored.
        /// </summary>
        public const string Unmatched = "unmatched";
    }

    /// <summary>
    /// The results of <see cref="InboundDuration"/>.
    /// </summary>
    public static class InboundResults
    {
        /// <summary>
        /// The message was recorded in a thread and the thread was routed.
        /// </summary>
        public const string Routed = "routed";

        /// <summary>
        /// The thread already held the message, so it was not recorded again.
        /// </summary>
        public const string Duplicate = "duplicate";

        /// <summary>
        /// An automated (AI) conversation owns the contact, so the message was left to it.
        /// </summary>
        public const string Automated = "automated";

        /// <summary>
        /// The message started an automated (AI) conversation, which answers it.
        /// </summary>
        public const string AIStarted = "ai_started";

        /// <summary>
        /// No enabled channel serves the message's channel, so it was dropped.
        /// </summary>
        public const string UnknownChannel = "unknown_channel";

        /// <summary>
        /// The message lacked its service or contact address, so it was dropped.
        /// </summary>
        public const string MissingAddress = "missing_address";

        /// <summary>
        /// No channel endpoint answers the service address, so the message was dropped.
        /// </summary>
        public const string NoEndpoint = "no_endpoint";

        /// <summary>
        /// The conversation lock was not acquired in time; the delivery is retried.
        /// </summary>
        public const string LockTimeout = "lock_timeout";
    }
}
