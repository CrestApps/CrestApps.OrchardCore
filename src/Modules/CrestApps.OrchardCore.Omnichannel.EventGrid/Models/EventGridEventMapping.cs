namespace CrestApps.OrchardCore.Omnichannel.EventGrid.Models;

/// <summary>
/// The result of mapping one Event Grid event onto the Omnichannel pipeline.
/// </summary>
public sealed class EventGridEventMapping
{
    /// <summary>
    /// Gets how the event maps onto the Omnichannel pipeline.
    /// </summary>
    public EventGridEventMappingKind Kind { get; init; }

    /// <summary>
    /// Gets the Omnichannel channel the event belongs to, when it is mapped.
    /// </summary>
    public string Channel { get; init; }

    /// <summary>
    /// Gets the internal Omnichannel event name the event is raised as, when it is routed.
    /// </summary>
    public string EventName { get; init; }

    /// <summary>
    /// Gets the provider's own identifier for the message.
    /// </summary>
    public string ProviderMessageId { get; init; }

    /// <summary>
    /// Gets the sender address.
    /// </summary>
    public string From { get; init; }

    /// <summary>
    /// Gets the recipient address.
    /// </summary>
    public string To { get; init; }

    /// <summary>
    /// Gets the message text.
    /// </summary>
    public string Content { get; init; }

    /// <summary>
    /// Gets the UTC time the provider received the message or the report, when it reported one.
    /// </summary>
    public DateTime? ReceivedUtc { get; init; }

    /// <summary>
    /// Gets the provider's delivery status, for a delivery report.
    /// </summary>
    public string DeliveryStatus { get; init; }

    /// <summary>
    /// Gets the provider's delivery status details, for a delivery report.
    /// </summary>
    public string DeliveryStatusDetails { get; init; }

    /// <summary>
    /// Gets why a recognized event could not be mapped, for a malformed event.
    /// </summary>
    public string Reason { get; init; }
}
