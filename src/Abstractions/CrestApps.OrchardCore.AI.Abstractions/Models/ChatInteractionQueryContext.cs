using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Context for querying chat interactions.
/// </summary>
public class ChatInteractionQueryContext : QueryContext
{
    /// <summary>
    /// Gets or sets the user ID to filter interactions by.
    /// </summary>
    public string UserId { get; set; }
}
