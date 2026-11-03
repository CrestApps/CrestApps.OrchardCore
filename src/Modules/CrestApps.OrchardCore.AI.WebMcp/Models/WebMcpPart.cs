using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// Exposes selected AI tools through WebMCP on the page of the content item it is attached to, in addition to the
/// tools the WebMCP settings give to every page.
/// </summary>
public sealed class WebMcpPart : ContentPart
{
    /// <summary>
    /// Gets or sets the names of the selectable tools and tool instances the content item's page exposes.
    /// </summary>
    public IList<string> Tools { get; set; } = [];
}
