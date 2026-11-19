using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.ContentFields.Taxonomies;

/// <summary>
/// The terms of an Orchard Core taxonomy as a flat list in tree order, each with its parent and the terms above it,
/// read from the taxonomy's stored JSON (<c>TaxonomyPart.Terms</c>, children under each term's <c>Terms</c>). It lets
/// modules walk a taxonomy without referencing the Taxonomies module.
/// </summary>
public sealed class TaxonomyTermTree
{
    private readonly Dictionary<string, TaxonomyTermNode> _byId;

    private TaxonomyTermTree(List<TaxonomyTermNode> terms)
    {
        Nodes = terms;
        _byId = new Dictionary<string, TaxonomyTermNode>(StringComparer.Ordinal);

        foreach (var term in terms)
        {
            _byId.TryAdd(term.Id, term);
        }
    }

    /// <summary>
    /// Gets the terms, in tree order (a parent before its children, siblings in their stored order).
    /// </summary>
    public IReadOnlyList<TaxonomyTermNode> Nodes { get; }

    /// <summary>
    /// Reads the tree of a taxonomy content item.
    /// </summary>
    /// <param name="taxonomy">The taxonomy, or <see langword="null"/>.</param>
    /// <returns>The tree; empty when the item has no terms.</returns>
    public static TaxonomyTermTree From(ContentItem taxonomy)
    {
        var terms = new List<TaxonomyTermNode>();

        if (((JsonObject)taxonomy?.Content)?["TaxonomyPart"]?["Terms"] is JsonArray roots)
        {
            Add(terms, roots, null, []);
        }

        return new TaxonomyTermTree(terms);
    }

    /// <summary>
    /// Finds a term by its content item id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The term, or <see langword="null"/>.</returns>
    public TaxonomyTermNode Find(string id)
        => id is not null && _byId.TryGetValue(id, out var term) ? term : null;

    /// <summary>
    /// Gets the children of a term, or the top terms.
    /// </summary>
    /// <param name="parentId">The parent's id, or <see langword="null"/> for the top.</param>
    /// <returns>The children, in order.</returns>
    public IEnumerable<TaxonomyTermNode> ChildrenOf(string parentId)
        => Nodes.Where(term => string.Equals(term.ParentId, parentId, StringComparison.Ordinal));

    /// <summary>
    /// Gets the names of a term and the terms above it, top first, such as "Hardware > Laptop".
    /// </summary>
    /// <param name="id">The term's id.</param>
    /// <param name="separator">What goes between the names.</param>
    /// <returns>The path, or <see langword="null"/> for a term the taxonomy doesn't have.</returns>
    public string GetPathText(string id, string separator = " > ")
    {
        if (Find(id) is not { } term)
        {
            return null;
        }

        return string.Join(separator, term.AncestorIds.Reverse().Select(ancestor => Find(ancestor)?.Name).Append(term.Name));
    }

    private static void Add(List<TaxonomyTermNode> terms, JsonArray items, string parentId, string[] ancestors)
    {
        foreach (var item in items.OfType<JsonObject>())
        {
            var id = item["ContentItemId"]?.GetValue<string>();

            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var name = item["DisplayText"]?.GetValue<string>() ?? item["TitlePart"]?["Title"]?.GetValue<string>() ?? id;
            var node = new TaxonomyTermNode(id, name, parentId, ancestors.Length, ancestors, item);

            terms.Add(node);

            if (item["Terms"] is JsonArray children)
            {
                Add(terms, children, id, [id, .. ancestors]);
            }
        }
    }
}

/// <summary>
/// One term of a <see cref="TaxonomyTermTree"/>.
/// </summary>
/// <param name="Id">The term's content item id.</param>
/// <param name="Name">Its display text.</param>
/// <param name="ParentId">The term above it, or <see langword="null"/> at the top.</param>
/// <param name="Level">Its depth, 0 at the top.</param>
/// <param name="AncestorIds">The terms above it, parent first.</param>
/// <param name="Content">The term's stored JSON, to read its parts.</param>
public sealed record TaxonomyTermNode(string Id, string Name, string ParentId, int Level, IReadOnlyList<string> AncestorIds, JsonObject Content);
