namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Result from paginated chat interaction queries.
/// </summary>
public class ChatInteractionResult
{
    /// <summary>
    /// Gets or sets the total count of interactions.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the list of interactions.
    /// </summary>
    public IEnumerable<ChatInteraction> Interactions { get; set; } = [];
}
