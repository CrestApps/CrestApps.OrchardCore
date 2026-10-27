namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// The topics an AI profile refuses, checked by the utility model on what the customer says, on the AI's replies, or
/// both, on top of Prompt Security.
/// </summary>
public sealed class AIProfileGuardrailSettings
{
    /// <summary>
    /// Gets or sets whether what the customer says is checked before the AI answers it.
    /// </summary>
    public bool CheckCustomer { get; set; }

    /// <summary>
    /// Gets or sets whether the AI's replies are checked before they are sent.
    /// </summary>
    public bool CheckReplies { get; set; }

    /// <summary>
    /// Gets or sets the categories refused, from <see cref="AIGuardrailCategories"/>.
    /// </summary>
    public string[] Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets other topics refused, described in a sentence or two.
    /// </summary>
    public string OtherTopics { get; set; }

    /// <summary>
    /// Gets or sets what the AI says instead when a guardrail triggers. Empty uses a default.
    /// </summary>
    public string PlaceholderReply { get; set; }

    /// <summary>
    /// Gets whether anything is refused.
    /// </summary>
    public bool HasTopics
        => (Categories?.Length ?? 0) > 0 || !string.IsNullOrWhiteSpace(OtherTopics);
}
