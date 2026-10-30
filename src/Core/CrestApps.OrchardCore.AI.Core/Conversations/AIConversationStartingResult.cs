namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// What the handlers of a starting conversation decided: variables to start with, a different profile, or not to
/// hold the conversation at all.
/// </summary>
public sealed class AIConversationStartingResult
{
    /// <summary>
    /// Gets the variables to start the conversation with.
    /// </summary>
    public Dictionary<string, string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the AI profile to answer with instead of the one the channel chose, or <see langword="null"/>
    /// to keep it.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the conversation should not take place: an outbound call or text is
    /// not sent, and an inbound one goes to the channel's fallback.
    /// </summary>
    public bool Declined { get; set; }

    /// <summary>
    /// Gets or sets why the conversation was declined, for the record.
    /// </summary>
    public string DeclineReason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a handler ran out of time, so the conversation started with what
    /// was decided by then.
    /// </summary>
    public bool TimedOut { get; set; }
}
