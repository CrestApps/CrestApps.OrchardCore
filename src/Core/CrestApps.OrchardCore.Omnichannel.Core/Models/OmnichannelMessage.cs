using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents a message exchanged through an omnichannel communication channel.
/// </summary>
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

    // ---- SMS Communication Portal fields ----
    // These extend the shared message/bubble in place (rather than introducing a separate SMS message entity)
    // so the human portal and the existing inbound-persistence and automated-AI paths all read one record type.

    /// <summary>
    /// Gets or sets the identifier of the <c>SmsConversation</c> (thread) this message belongs to. Indexed so a
    /// thread loads its bubbles by conversation. Null for messages not yet linked to a portal conversation.
    /// </summary>
    public string ConversationId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent who composed an outbound message. Null for inbound and
    /// automated (AI) messages.
    /// </summary>
    public string SentByAgentId { get; set; }

    /// <summary>
    /// Gets or sets the normalized delivery status of an outbound message, stored as the string name of the
    /// portal's delivery-status enumeration. Null for inbound/automated messages, which carry no delivery
    /// lifecycle.
    /// </summary>
    public string DeliveryStatus { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for this message, used to correlate delivery receipts.
    /// </summary>
    public string ProviderMessageId { get; set; }

    /// <summary>
    /// Gets or sets the internal references to any ingested MMS media for this message. Provider-hosted media
    /// URLs are reference metadata only; the durable copies live in the encrypted media store.
    /// </summary>
    public IList<string> MediaReferences { get; set; } = [];

    /// <summary>
    /// Gets or sets the provider error code when an outbound message failed.
    /// </summary>
    public string ErrorCode { get; set; }
}
