namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Marks an AI session whose starting handlers have run, so they run once per conversation.
/// </summary>
public sealed class AIConversationStartedMetadata
{
    /// <summary>
    /// Gets or sets when the starting handlers ran.
    /// </summary>
    public DateTime StartedUtc { get; set; }
}
