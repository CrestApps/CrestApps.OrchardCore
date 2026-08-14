using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;

/// <summary>
/// Represents the chat interaction list options.
/// </summary>
public class ChatInteractionListOptions
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string SearchText { get; set; }

    /// <summary>
    /// Gets or sets the route values.
    /// </summary>
    public RouteValueDictionary RouteValues { get; set; } = [];
}
