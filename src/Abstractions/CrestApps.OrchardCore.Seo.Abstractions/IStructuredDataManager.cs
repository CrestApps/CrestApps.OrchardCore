using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// Builds schema.org structured data. The same data is written in the JSON-LD script of a page and returned to
/// agents, so both always describe a content item the same way.
/// </summary>
public interface IStructuredDataManager
{
    /// <summary>
    /// Builds the JSON-LD document of a page: the site-wide nodes and, when there is one, the node of the page's
    /// content item.
    /// </summary>
    /// <param name="contentItem">The content item the page displays, or <see langword="null"/>.</param>
    /// <param name="additionalNodes">Other nodes to include, such as nodes a theme added to the page.</param>
    /// <returns>The document, or <see langword="null"/> when there is nothing to write.</returns>
    Task<JsonObject> BuildPageDocumentAsync(ContentItem contentItem, IEnumerable<JsonObject> additionalNodes = null);

    /// <summary>
    /// Builds the schema.org object that describes one content item, with its own <c>@context</c>.
    /// </summary>
    /// <param name="contentItem">The content item.</param>
    /// <param name="purpose">What the object is built for.</param>
    /// <returns>The object, or <see langword="null"/> when the content type publishes no structured data.</returns>
    Task<JsonObject> BuildEntityAsync(ContentItem contentItem, StructuredDataPurpose purpose);
}
