namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents a provider-agnostic inbound voice event after a telephony provider or PBX webhook has
/// been normalized. It is the entry point the Contact Center uses to create an interaction, resolve
/// CRM context, queue the work, and route it to an available agent.
/// </summary>
public sealed class InboundVoiceEvent
{
    /// <summary>
    /// Gets or sets the technical name of the provider that produced the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific identifier of the inbound call.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the address of the caller (the customer's phone number).
    /// </summary>
    public string FromAddress { get; set; }

    /// <summary>
    /// Gets or sets the address the call was placed to (the dialed number or DID that identifies the channel endpoint).
    /// </summary>
    public string ToAddress { get; set; }

    /// <summary>
    /// Gets or sets the optional caller display name supplied by the provider.
    /// </summary>
    public string CallerName { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the inbound call was received. When not supplied, the current time is used.
    /// </summary>
    public DateTime? ReceivedUtc { get; set; }

    /// <summary>
    /// Gets or sets additional provider metadata to retain on the interaction for troubleshooting.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
