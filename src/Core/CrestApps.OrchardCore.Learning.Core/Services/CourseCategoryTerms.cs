using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentFields.Taxonomies;
using OrchardCore;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Finds and adds course categories in a taxonomy's stored terms (<c>TaxonomyPart.Terms</c>, children under each
/// term's <c>Terms</c>), the way Orchard Core keeps them.
/// </summary>
public static class CourseCategoryTerms
{
    /// <summary>
    /// Finds a category, or adds it. One name is looked for anywhere in the tree (the first in tree order), and added at
    /// the top when no term has it; a path of names is followed from the top, adding the levels that are missing. Names
    /// are compared without case.
    /// </summary>
    /// <param name="taxonomy">The taxonomy; its JSON changes when a term is added.</param>
    /// <param name="names">The name, or the path of names from the top.</param>
    /// <param name="termContentType">The content type of a term that is added.</param>
    /// <param name="added">Whether a term was added.</param>
    /// <returns>The id of the category, or <see langword="null"/> when no name was given.</returns>
    public static string FindOrAdd(ContentItem taxonomy, IReadOnlyList<string> names, string termContentType, out bool added)
    {
        ArgumentNullException.ThrowIfNull(taxonomy);

        added = false;

        var path = (names ?? []).Select(name => name?.Trim()).Where(name => !string.IsNullOrEmpty(name)).ToList();

        if (path.Count == 0)
        {
            return null;
        }

        if (path.Count == 1 && TaxonomyTermTree.From(taxonomy).Nodes.FirstOrDefault(node => Same(node.Name, path[0])) is { } existing)
        {
            return existing.Id;
        }

        var level = RootTerms(taxonomy);
        string id = null;

        foreach (var name in path)
        {
            var term = level.OfType<JsonObject>().FirstOrDefault(candidate => Same(NameOf(candidate), name));

            if (term is null)
            {
                term = Term(IdGenerator.GenerateId(), name, termContentType);
                level.Add(term);
                added = true;
            }

            id = term["ContentItemId"]?.GetValue<string>();

            if (term["Terms"] is not JsonArray children)
            {
                children = [];
                term["Terms"] = children;
            }

            level = children;
        }

        return id;
    }

    /// <summary>
    /// Gets a category and every category under it, so choosing a category lists the courses of its sub-categories too.
    /// </summary>
    /// <param name="tree">The categories.</param>
    /// <param name="categoryId">The category.</param>
    /// <returns>The ids, the category first; empty when the category does not exist.</returns>
    public static IReadOnlyList<string> WithDescendants(TaxonomyTermTree tree, string categoryId)
    {
        ArgumentNullException.ThrowIfNull(tree);

        if (tree.Find(categoryId) is null)
        {
            return [];
        }

        return [.. tree.Nodes
            .Where(node => node.Id == categoryId || node.AncestorIds.Contains(categoryId, StringComparer.Ordinal))
            .Select(node => node.Id)];
    }

    private static JsonArray RootTerms(ContentItem taxonomy)
    {
        var content = (JsonObject)taxonomy.Content;

        if (content["TaxonomyPart"] is not JsonObject part)
        {
            part = [];
            content["TaxonomyPart"] = part;
        }

        if (part["Terms"] is not JsonArray terms)
        {
            terms = [];
            part["Terms"] = terms;
        }

        return terms;
    }

    private static string NameOf(JsonObject term)
        => term["DisplayText"]?.GetValue<string>() ?? term["TitlePart"]?["Title"]?.GetValue<string>();

    private static bool Same(string left, string right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    // A term as a taxonomy keeps it: a content item of the term type, with its title.
    private static JsonObject Term(string id, string name, string contentType)
        => new()
        {
            ["ContentItemId"] = id,
            ["ContentItemVersionId"] = IdGenerator.GenerateId(),
            ["ContentType"] = contentType,
            ["DisplayText"] = name,
            ["Latest"] = true,
            ["Published"] = true,
            ["TitlePart"] = new JsonObject { ["Title"] = name },
            ["Terms"] = new JsonArray(),
        };
}
