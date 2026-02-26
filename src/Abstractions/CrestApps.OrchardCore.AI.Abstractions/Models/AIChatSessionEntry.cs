namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// A lightweight representation of an AI chat session used for listing purposes.
/// Contains only the fields needed to display session summaries without loading the full document.
/// </summary>
public sealed class AIChatSessionEntry
{
    public string SessionId { get; set; }

    public string ProfileId { get; set; }

    public string Title { get; set; }

    public string UserId { get; set; }

    public string ClientId { get; set; }

    public ChatSessionStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime LastActivityUtc { get; set; }
}
