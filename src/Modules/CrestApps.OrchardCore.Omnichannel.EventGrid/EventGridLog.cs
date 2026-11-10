using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.EventGrid;

/// <summary>
/// The log messages of the Azure Event Grid webhook, with the event ids 18400 to 18449 of the messaging range.
/// The messages are the ones the webhook logged before, with event ids added.
/// </summary>
internal static partial class EventGridLog
{
    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 400,
        Level = LogLevel.Warning,
        Message = "Rejected an Event Grid subscription validation event {EventId} that carried no validation code.")]
    public static partial void SubscriptionValidationWithoutCode(this ILogger logger, string eventId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 401,
        Level = LogLevel.Information,
        Message = "Event Grid subscription validation received for event {EventId}; answering with the validation code.")]
    public static partial void SubscriptionValidationReceived(this ILogger logger, string eventId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 402,
        Level = LogLevel.Information,
        Message = "Event Grid event received. EventType: {EventType}, EventId: {EventId}.")]
    public static partial void EventReceived(this ILogger logger, string eventType, string eventId);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 403,
        Level = LogLevel.Information,
        Message = "Event Grid event {EventId} is an SMS delivery report for message {ProviderMessageId} with status '{DeliveryStatus}' ('{DeliveryStatusDetails}'). Delivery reports are stored but not routed; the sent message's delivery status is not updated.")]
    public static partial void DeliveryReportStored(this ILogger logger, string eventId, string providerMessageId, string deliveryStatus, string deliveryStatusDetails);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 404,
        Level = LogLevel.Warning,
        Message = "Event Grid event {EventId} of type {EventType} could not be mapped because {Reason}. It is stored with its raw event type and not routed to a channel.")]
    public static partial void EventMalformed(this ILogger logger, string eventId, string eventType, string reason);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 405,
        Level = LogLevel.Debug,
        Message = "Event Grid event {EventId} of type {EventType} has no Omnichannel mapping. It is stored with its raw event type and passed to the handlers unchanged.")]
    public static partial void EventUnmapped(this ILogger logger, string eventId, string eventType);

    [LoggerMessage(
        EventId = CrestAppsEventIds.Messaging + 406,
        Level = LogLevel.Information,
        Message = "Mapped Event Grid event {EventId} of type {EventType} to channel {Channel} and event {OmnichannelEvent}. ProviderMessageId: {ProviderMessageId}, Length: {Length}.")]
    public static partial void EventMapped(this ILogger logger, string eventId, string eventType, string channel, string omnichannelEvent, string providerMessageId, int length);
}
