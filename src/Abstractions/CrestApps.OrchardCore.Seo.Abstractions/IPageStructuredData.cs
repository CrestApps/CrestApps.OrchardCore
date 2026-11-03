using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// The structured data of the page the current request renders. It is scoped to the request: the content item the
/// page displays is recorded on it, and code, such as a controller, a Razor view or a Liquid template, can describe
/// the page and add nodes before the page's JSON-LD script is written.
/// </summary>
/// <example>
/// A hard-coded page, such as an About page served by a controller, describes itself:
/// <code>
/// pageStructuredData.MainEntity = JsonLd.CreateNode("AboutPage");
/// pageStructuredData.MainEntity["name"] = "About Contoso";
/// pageStructuredData.Add(JsonLd.CreateNode("Person", "#founder"));
/// </code>
/// </example>
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
    /// Gets or sets the node that describes the page itself, such as an <c>AboutPage</c> or an <c>Event</c>, for a
    /// page code renders. It gets the page's URL and the site's publisher like a content item's node. On a page that
    /// displays a described content item, its properties are added to that item's node.
    /// </summary>
    JsonObject MainEntity { get; set; }

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
