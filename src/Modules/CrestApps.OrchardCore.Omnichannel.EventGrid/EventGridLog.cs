using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.EventGrid;

/// <summary>
/// The log messages of the Azure Event Grid webhook, with the event ids 18400 to 18449 of the messaging range. An Event
/// Grid event's own id is logged as <c>EventGridEventId</c>, so it never shadows the <c>EventId</c> of the log entry.
/// </summary>
internal static partial class EventGridLog
{
    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 400,
        Level = LogLevel.Warning,
        Message = "Rejected an Event Grid subscription validation event {EventGridEventId} that carried no validation code.")]
    public static partial void SubscriptionValidationWithoutCode(this ILogger logger, string eventGridEventId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 401,
        Level = LogLevel.Information,
        Message = "Event Grid subscription validation received for event {EventGridEventId}; answering with the validation code.")]
    public static partial void SubscriptionValidationReceived(this ILogger logger, string eventGridEventId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 402,
        Level = LogLevel.Information,
        Message = "Event Grid event received. EventType: {EventType}, EventGridEventId: {EventGridEventId}.")]
    public static partial void EventReceived(this ILogger logger, string eventType, string eventGridEventId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 403,
        Level = LogLevel.Information,
        Message = "Event Grid event {EventGridEventId} is an SMS delivery report for message {ProviderMessageId} with status '{DeliveryStatus}' ('{DeliveryStatusDetails}'). Delivery reports are stored but not routed; the sent message's delivery status is not updated.")]
    public static partial void DeliveryReportStored(this ILogger logger, string eventGridEventId, string providerMessageId, string deliveryStatus, string deliveryStatusDetails);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 404,
        Level = LogLevel.Warning,
        Message = "Event Grid event {EventGridEventId} of type {EventType} could not be mapped because {Reason}. It is stored with its raw event type and not routed to a channel.")]
    public static partial void EventMalformed(this ILogger logger, string eventGridEventId, string eventType, string reason);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 405,
        Level = LogLevel.Debug,
        Message = "Event Grid event {EventGridEventId} of type {EventType} has no Omnichannel mapping. It is stored with its raw event type and passed to the handlers unchanged.")]
    public static partial void EventUnmapped(this ILogger logger, string eventGridEventId, string eventType);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 406,
        Level = LogLevel.Information,
        Message = "Mapped Event Grid event {EventGridEventId} of type {EventType} to channel {Channel} and event {OmnichannelEvent}. ProviderMessageId: {ProviderMessageId}, Length: {Length}.")]
    public static partial void EventMapped(this ILogger logger, string eventGridEventId, string eventType, string channel, string omnichannelEvent, string providerMessageId, int length);
}
