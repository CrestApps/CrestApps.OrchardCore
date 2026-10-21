using System.Globalization;
using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;
using OrchardCore.Modules.Services;

namespace CrestApps.OrchardCore.Products.Services.Attributes;

/// <summary>
/// Loads taxonomies once per scope and answers where a term sits in its tree.
/// </summary>
/// <remarks>
/// A rebuild indexes many products that use the same few taxonomies, so each taxonomy is read and flattened
/// once. Terms are read from the taxonomy's stored JSON (nested <c>Terms</c> arrays), which keeps the Products
/// module free of a reference to the Taxonomies module.
/// </remarks>
public sealed class TaxonomyTermLookup
{
    private readonly IContentManager _contentManager;
    private readonly ISlugService _slugService;
    private readonly Dictionary<string, Dictionary<string, TaxonomyTerm>> _taxonomies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _taxonomyNames = new(StringComparer.Ordinal);

    public TaxonomyTermLookup(
        IContentManager contentManager,
        ISlugService slugService)
    {
        _contentManager = contentManager;
        _slugService = slugService;
    }

    /// <summary>
    /// Gets a term and its ancestors, from the top-level term down to the term itself.
    /// </summary>
    /// <param name="taxonomyContentItemId">The taxonomy.</param>
    /// <param name="termContentItemId">The term.</param>
    public async Task<IReadOnlyList<TaxonomyTerm>> GetPathAsync(string taxonomyContentItemId, string termContentItemId)
    {
        var terms = await GetTermsAsync(taxonomyContentItemId);

        if (terms is null || !terms.TryGetValue(termContentItemId, out var term))
        {
            return [];
        }

        var path = new List<TaxonomyTerm>();

        while (term is not null)
        {
            path.Insert(0, term);
            term = term.ParentId is not null && terms.TryGetValue(term.ParentId, out var parent) ? parent : null;
        }

        return path;
    }

    /// <summary>
    /// Gets the name of a taxonomy, or <see langword="null"/> when there's no such published taxonomy.
    /// </summary>
    /// <param name="taxonomyContentItemId">The taxonomy.</param>
    public async Task<string> GetTaxonomyNameAsync(string taxonomyContentItemId)
    {
        if (await GetTermsAsync(taxonomyContentItemId) is null)
        {
            return null;
        }

        return _taxonomyNames.GetValueOrDefault(taxonomyContentItemId);
    }

    /// <summary>
    /// Gets every term of a taxonomy, keyed by term content item identifier.
    /// </summary>
    /// <param name="taxonomyContentItemId">The taxonomy.</param>
    public async Task<IReadOnlyDictionary<string, TaxonomyTerm>> GetTermsAsync(string taxonomyContentItemId)
    {
        if (string.IsNullOrEmpty(taxonomyContentItemId))
        {
            return null;
        }

        if (_taxonomies.TryGetValue(taxonomyContentItemId, out var cached))
        {
            return cached;
        }

        var terms = new Dictionary<string, TaxonomyTerm>(StringComparer.Ordinal);
        var taxonomy = await _contentManager.GetAsync(taxonomyContentItemId, VersionOptions.Published);

        if (taxonomy is not null &&
            ((JsonObject)taxonomy.Content)["TaxonomyPart"]?["Terms"] is JsonArray roots)
        {
            var order = 0;
            Flatten(roots, null, 1, terms, ref order);
            AssignSlugs(terms);
        }

        _taxonomies[taxonomyContentItemId] = terms;
        _taxonomyNames[taxonomyContentItemId] = taxonomy?.DisplayText;

        return terms;
    }

    /// <summary>
    /// Gives every term a readable slug for listing URLs, unique within its taxonomy: the slug of its name, or,
    /// when two terms share a name (Accessories under Men and under Women), its parent's slug and its own.
    /// </summary>
    private void AssignSlugs(Dictionary<string, TaxonomyTerm> terms)
    {
        var ordered = terms.Values.OrderBy(term => term.Order).ToArray();
        var counts = ordered
            .GroupBy(term => Slugify(term.DisplayText, term.ContentItemId), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var term in ordered)
        {
            var slug = Slugify(term.DisplayText, term.ContentItemId);

            if (counts[slug] > 1 && term.ParentId is not null && terms.TryGetValue(term.ParentId, out var parent) && parent.Slug is not null)
            {
                slug = parent.Slug + "-" + slug;
            }

            var candidate = slug;

            for (var suffix = 2; !used.Add(candidate); suffix++)
            {
                candidate = slug + "-" + suffix.ToString(CultureInfo.InvariantCulture);
            }

            terms[term.ContentItemId] = term with { Slug = candidate };
        }
    }

    /// <summary>
    /// Turns a term name into a URL slug with Orchard Core's slug service: <c>Pickup Trucks</c> becomes
    /// <c>pickup-trucks</c> and <c>Mercedes-Benz</c> becomes <c>mercedes-benz</c>. Accents are dropped.
    /// </summary>
    /// <param name="text">The term name.</param>
    /// <param name="fallback">The slug of a name with no letters or digits.</param>
    public string Slugify(string text, string fallback)
    {
        var slug = string.IsNullOrWhiteSpace(text) ? null : _slugService.Slugify(text);

        return string.IsNullOrEmpty(slug) ? fallback : slug;
    }

    private static void Flatten(JsonArray items, string parentId, int level, Dictionary<string, TaxonomyTerm> terms, ref int order)
    {
        foreach (var item in items.OfType<JsonObject>())
        {
            var id = item["ContentItemId"]?.GetValue<string>();

            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            terms[id] = new TaxonomyTerm(id, item["DisplayText"]?.GetValue<string>() ?? id, parentId, level, order++)
            {
                Content = item,
            };

            if (item["Terms"] is JsonArray children)
            {
                Flatten(children, id, level + 1, terms, ref order);
            }
        }
    }
}

/// <summary>
/// A taxonomy term and where it sits in its tree.
/// </summary>
/// <param name="ContentItemId">The term's content item identifier.</param>
/// <param name="DisplayText">The term's name.</param>
/// <param name="ParentId">The parent term, or <see langword="null"/> for a top-level term.</param>
/// <param name="Level">The term's depth: 1 for a top-level term.</param>
/// <param name="Order">The term's position in a depth-first walk of the taxonomy.</param>
public sealed record TaxonomyTerm(string ContentItemId, string DisplayText, string ParentId, int Level, int Order)
{
    /// <summary>
    /// The term's slug in listing URLs, unique within its taxonomy.
    /// </summary>
    public string Slug { get; init; }

    /// <summary>
    /// The term's stored content item, with its parts and fields.
    /// </summary>
    public JsonObject Content { get; init; }
}
