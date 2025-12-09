namespace CrestApps.OrchardCore.AI.Chat.Models;

/// <summary>
/// Represents a chat message entry sent from the client for conversation history.
/// </summary>
public sealed class ChatMessageEntry
{
    public string Role { get; set; }

    public string Content { get; set; }
}
