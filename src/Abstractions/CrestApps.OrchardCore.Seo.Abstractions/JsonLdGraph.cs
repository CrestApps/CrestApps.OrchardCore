using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Seo;

/// <summary>
/// The nodes of a JSON-LD document. Nodes that share an <c>@id</c> are merged into one node, so several providers can
/// describe the same thing without writing it twice.
/// </summary>
public sealed class JsonLdGraph
{
    private readonly List<JsonObject> _nodes = [];
    private readonly Dictionary<string, JsonObject> _nodesById = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the nodes in the order they were first added.
    /// </summary>
    public IReadOnlyList<JsonObject> Nodes => _nodes;

    /// <summary>
    /// Adds a node to the graph. When a node with the same <c>@id</c> is already in the graph, the properties of
    /// <paramref name="node"/> are merged into it and replace the values it already had.
    /// </summary>
    /// <param name="node">The node to add.</param>
    /// <returns>The node held by the graph.</returns>
    public JsonObject Add(JsonObject node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var id = JsonLd.GetString(node, StructuredDataConstants.Keywords.Id);

        if (id is not null && _nodesById.TryGetValue(id, out var existing))
        {
            if (!ReferenceEquals(existing, node))
            {
                JsonLd.Merge(existing, node);
            }

            return existing;
        }

        if (_nodes.Contains(node))
        {
            return node;
        }

        _nodes.Add(node);

        if (id is not null)
        {
            _nodesById[id] = node;
        }

        return node;
    }

    /// <summary>
    /// Finds the node with the given <c>@id</c>.
    /// </summary>
    /// <param name="id">The node identifier.</param>
    /// <returns>The node, or <see langword="null"/> when the graph has none with that identifier.</returns>
    public JsonObject Find(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        return _nodesById.GetValueOrDefault(id);
    }

    /// <summary>
    /// Removes a node from the graph.
    /// </summary>
    /// <param name="node">The node to remove.</param>
    /// <returns><see langword="true"/> when the node was in the graph.</returns>
    public bool Remove(JsonObject node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!_nodes.Remove(node))
        {
            return false;
        }

        var id = JsonLd.GetString(node, StructuredDataConstants.Keywords.Id);

        if (id is not null)
        {
            _nodesById.Remove(id);
        }

        return true;
    }

    /// <summary>
    /// Builds the JSON-LD document: <c>{ "@context": "https://schema.org", "@graph": [...] }</c>.
    /// </summary>
    /// <returns>The document, or <see langword="null"/> when the graph is empty.</returns>
    public JsonObject ToDocument()
    {
        if (_nodes.Count == 0)
        {
            return null;
        }

        var graph = new JsonArray();

        foreach (var node in _nodes)
        {
            var copy = (JsonObject)node.DeepClone();
            copy.Remove(StructuredDataConstants.Keywords.Context);
            graph.Add(copy);
        }

        return new JsonObject
        {
            [StructuredDataConstants.Keywords.Context] = StructuredDataConstants.SchemaOrgContext,
            [StructuredDataConstants.Keywords.Graph] = graph,
        };
    }
}
