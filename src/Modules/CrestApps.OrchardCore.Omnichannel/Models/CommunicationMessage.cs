namespace CrestApps.OrchardCore.Omnichannel.Models;

/// <summary>
/// Represents the incoming ACS message payload (SMS, Chat, etc.)
/// </summary>
internal sealed class CommunicationMessage
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the channel.
    /// </summary>
    public string Channel { get; set; }  // "SMS", "Chat", "Teams", etc.

    /// <summary>
    /// Gets or sets the from.
    /// </summary>
    public string From { get; set; }     // Customer phone/email/userId

    /// <summary>
    /// Gets or sets the to.
    /// </summary>
    public string To { get; set; }       // Service number/email/bot

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string Type { get; set; }     // "Message", "DeliveryReport", etc.

    /// <summary>
    /// Gets or sets the content.
    /// </summary>
    public string Content { get; set; }  // Text content

    /// <summary>
    /// Gets or sets the timestamp.
    /// </summary>
    public DateTime? Timestamp { get; set; }
}
