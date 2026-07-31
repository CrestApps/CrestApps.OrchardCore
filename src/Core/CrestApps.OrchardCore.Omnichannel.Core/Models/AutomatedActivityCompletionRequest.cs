namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the final result of an AI-driven automated activity conversation.
/// </summary>
public sealed class AutomatedActivityCompletionRequest
{
    /// <summary>
    /// Gets or sets the activity completed by the AI conversation.
    /// </summary>
    public OmnichannelActivity Activity { get; set; }

    /// <summary>
    /// Gets or sets the AI chat session identifier containing the full conversation.
    /// </summary>
    public string AISessionId { get; set; }

    /// <summary>
    /// Gets or sets the AI-generated conversation summary to append to the activity notes.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the disposition selected by the AI conclusion analysis.
    /// </summary>
    public string DispositionId { get; set; }

    /// <summary>
    /// Gets or sets the actor identifier recorded for the automated completion.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets the actor display name recorded for the automated completion.
    /// </summary>
    public string ActorDisplayName { get; set; }

    /// <summary>
    /// Gets or sets optional schedule dates for disposition-driven subject actions.
    /// </summary>
    public IDictionary<string, DateTime?> ActionScheduleDates { get; set; }
}
