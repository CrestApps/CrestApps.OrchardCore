namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Provides the context an AI assist provider uses to summarize an interaction or suggest a disposition.
/// </summary>
public sealed class AssistContext
{
    /// <summary>
    /// Gets or sets the interaction identifier the assistance relates to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the CRM activity identifier the assistance relates to.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the subject content type of the activity, used to scope disposition suggestions.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the conversation transcript, when available.
    /// </summary>
    public string Transcript { get; set; }

    /// <summary>
    /// Gets or sets the agent notes captured for the interaction.
    /// </summary>
    public string Notes { get; set; }
}
