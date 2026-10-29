namespace CrestApps.OrchardCore.Omnichannel.Automation;

/// <summary>
/// Remembers, on a customer's turn in an automated conversation's transcript, which provider message it was, so a handoff
/// can tell the human thread which messages it already holds.
/// </summary>
public sealed class AutomatedInboundPromptSource
{
    /// <summary>
    /// Gets or sets the provider's identifier of the customer's message.
    /// </summary>
    public string ProviderMessageId { get; set; }
}
