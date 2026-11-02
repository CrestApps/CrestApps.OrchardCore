using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// The structured data of the page the current request renders. It is scoped to the request: the content item the
/// page displays is recorded on it, and themes or modules can add nodes before the page's JSON-LD script is written.
/// </summary>
public interface IPageStructuredData
{
    /// <summary>
    /// Gets or sets the content item the page displays, or <see langword="null"/> for a page without one.
    /// </summary>
    ContentItem ContentItem { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the page writes no JSON-LD script.
    /// </summary>
    bool Suppressed { get; set; }

    /// <summary>
    /// Gets the nodes added to the page.
    /// </summary>
    IReadOnlyList<JsonObject> Nodes { get; }

    /// <summary>
    /// Adds a node to the page's JSON-LD, such as a <c>BreadcrumbList</c> or an <c>ItemList</c> built by a theme.
    /// </summary>
    /// <param name="node">The node.</param>
    void Add(JsonObject node);
}
