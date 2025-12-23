namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Context for querying chat interactions.
/// </summary>
public class ChatInteractionQueryContext
{
    /// <summary>
    /// Gets or sets the user ID to filter interactions by.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets a text filter for the interaction title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the source to filter interactions by.
    /// </summary>
    public string Source { get; set; }
}
