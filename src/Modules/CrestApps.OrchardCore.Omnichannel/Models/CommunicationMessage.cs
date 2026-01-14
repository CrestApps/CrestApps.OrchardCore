namespace CrestApps.OrchardCore.Omnichannel.Models;

/// <summary>
/// Represents the incoming ACS message payload (SMS, Chat, etc.)
/// </summary>
internal sealed class CommunicationMessage
{
    public string Id { get; set; }

    public string Channel { get; set; }  // "SMS", "Chat", "Teams", etc.

    public string From { get; set; }     // Customer phone/email/userId

    public string To { get; set; }       // Service number/email/bot

    public string Type { get; set; }     // "Message", "DeliveryReport", etc.

    public string Content { get; set; }  // Text content

    public DateTime? Timestamp { get; set; }
}
