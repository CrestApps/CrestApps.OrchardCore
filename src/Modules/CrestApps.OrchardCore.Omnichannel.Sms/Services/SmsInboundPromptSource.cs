namespace CrestApps.OrchardCore.Omnichannel.Sms.Services;

/// <summary>
/// Stored on a customer's prompt in an automated SMS conversation: the provider message the prompt was made from.
/// </summary>
internal sealed class SmsInboundPromptSource
{
    /// <summary>
    /// Gets or sets the provider's identifier for the inbound message.
    /// </summary>
    public string ProviderMessageId { get; set; }
}
