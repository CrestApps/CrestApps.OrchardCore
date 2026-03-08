using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class OmnichannelMessage : Entity
{
    /// <summary>
    /// A unique identifier for the message.
    /// Can be generated internally (e.g., GUID) or come from the provider.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// The channel through which the message was sent or received.
    /// Examples: "SMS", "Email", "Phone", "Chat".
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// The identity of the customer in this conversation.
    /// For SMS → phone number; for Email → email address; for Chat → user ID, etc.
    /// Always represents the customer regardless of direction.
    /// </summary>
    public string CustomerAddress { get; set; }

    /// <summary>
    /// The identity of your system, agent, or business endpoint.  
    /// For SMS → your sending phone number; for Email → your support address;  
    /// for Chat → bot or agent ID.
    /// Always represents your side regardless of direction.
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// The text content of the message.  
    /// For non-text channels (e.g., voice calls), this could store a transcription.  
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// The timestamp (UTC) when the message was sent or received.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Indicates the message direction:
    /// true = inbound (customer → service), false = outbound (service → customer).
    /// </summary>
    public bool IsInbound { get; set; }
}
