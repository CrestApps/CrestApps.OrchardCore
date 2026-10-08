namespace CrestApps.OrchardCore.Omnichannel.EventGrid.Models;

/// <summary>
/// Describes how an incoming Event Grid event maps onto the Omnichannel pipeline.
/// </summary>
public enum EventGridEventMappingKind
{
    /// <summary>
    /// The event type has no Omnichannel mapping. It is stored and passed to the handlers unchanged.
    /// </summary>
    Unmapped,

    /// <summary>
    /// The event is an inbound text message, raised as the platform's own SMS received event on the SMS channel.
    /// </summary>
    SmsReceived,

    /// <summary>
    /// The event is a delivery report for a text message that was sent. It is recognized but not routed.
    /// </summary>
    SmsDeliveryReport,

    /// <summary>
    /// The event type is recognized, but its data is missing fields the mapping needs. It is stored and passed to
    /// the handlers unchanged.
    /// </summary>
    Malformed,
}
