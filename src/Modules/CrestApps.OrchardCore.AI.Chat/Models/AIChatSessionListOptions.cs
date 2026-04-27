using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.AI.Chat.Models;

/// <summary>
/// Represents the AI chat session list options.
/// </summary>
public class AIChatSessionListOptions
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    [FromQuery(Name = "q")]
    public string SearchText { get; set; }

    /// <summary>
    /// Gets or sets the route values.
    /// </summary>
    [BindNever]
    public RouteValueDictionary RouteValues { get; set; } = [];
}
