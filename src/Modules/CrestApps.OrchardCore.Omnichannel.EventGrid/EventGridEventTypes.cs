namespace CrestApps.OrchardCore.Omnichannel.EventGrid;

/// <summary>
/// The Azure Event Grid event types the Omnichannel Event Grid endpoint recognizes.
/// </summary>
public static class EventGridEventTypes
{
    /// <summary>
    /// The handshake Event Grid sends before it starts delivering events to a new webhook subscription.
    /// </summary>
    public const string SubscriptionValidation = "Microsoft.EventGrid.SubscriptionValidationEvent";

    /// <summary>
    /// An Azure Communication Services inbound text message.
    /// </summary>
    public const string AcsSmsReceived = "Microsoft.Communication.SMSReceived";

    /// <summary>
    /// An Azure Communication Services delivery report for a text message that was sent.
    /// </summary>
    public const string AcsSmsDeliveryReportReceived = "Microsoft.Communication.SMSDeliveryReportReceived";
}
