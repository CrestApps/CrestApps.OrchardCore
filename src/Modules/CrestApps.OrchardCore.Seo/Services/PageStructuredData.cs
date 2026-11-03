using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Seo.Services;

/// <summary>
/// Holds the structured data of the page the current request renders.
/// </summary>
internal sealed class PageStructuredData : IPageStructuredData
{
    private readonly List<JsonObject> _nodes = [];

    /// <inheritdoc/>
    public ContentItem ContentItem { get; set; }

    /// <inheritdoc/>
    public bool Suppressed { get; set; }

    /// <inheritdoc/>
    public JsonObject MainEntity { get; set; }

    /// <inheritdoc/>
    public IReadOnlyList<JsonObject> Nodes => _nodes;

    /// <inheritdoc/>
    public void Add(JsonObject node)
    {
        ArgumentNullException.ThrowIfNull(node);

        _nodes.Add(node);
    }
}
