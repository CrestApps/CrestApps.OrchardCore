using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry;

/// <summary>
/// The log messages of the messaging workspace services, with the event ids 18000 to 18099 of the messaging range.
/// </summary>
internal static partial class MessagingLog
{
    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 0,
        Level = LogLevel.Warning,
        Message = "The outbound {Channel} message {MessageId} was given up on after {Attempts} attempt(s) ({Reason}).")]
    public static partial void OutboxMessageFailed(this ILogger logger, string channel, string messageId, int attempts, string reason);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 1,
        Level = LogLevel.Debug,
        Message = "The outbound {Channel} message {MessageId} was refused again on attempt {Attempts}; it is queued for another attempt.")]
    public static partial void OutboxMessageRescheduled(this ILogger logger, string channel, string messageId, int attempts);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 3,
        Level = LogLevel.Debug,
        Message = "A {Channel} delivery receipt for provider message {ProviderMessageId} with status {Status} matched no {Missing}; it was ignored.")]
    public static partial void DeliveryReceiptUnmatched(this ILogger logger, string channel, string providerMessageId, string status, string missing);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 4,
        Level = LogLevel.Information,
        Message = "Conversation {ConversationId} on {Channel} missed its first-response target by {OverdueSeconds} second(s).")]
    public static partial void FirstResponseBreached(this ILogger logger, string conversationId, string channel, long overdueSeconds);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 5,
        Level = LogLevel.Debug,
        Message = "Routed conversation {ConversationId} was not picked up in time and is now {AssignmentStatus}.")]
    public static partial void ConversationReassigned(this ILogger logger, string conversationId, string assignmentStatus);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 6,
        Level = LogLevel.Information,
        Message = "Broadcast {BroadcastId} on {Channel} was handed to the outreach engine as run {RunId}.")]
    public static partial void BroadcastStarted(this ILogger logger, string broadcastId, string channel, string runId);
}
