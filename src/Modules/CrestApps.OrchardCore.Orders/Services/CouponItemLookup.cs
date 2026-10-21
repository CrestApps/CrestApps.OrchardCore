using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Finds the products and categories a coupon can be limited to, by name.
/// </summary>
public sealed class CouponItemLookup
{
    private const string TaxonomyContentType = "Taxonomy";

    private readonly ISession _session;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public CouponItemLookup(ISession session, IContentDefinitionManager contentDefinitionManager)
    {
        _session = session;
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Returns the latest products whose title contains <paramref name="query"/>, or the first ones.
    /// </summary>
    public async Task<IReadOnlyList<CouponItem>> SearchProductsAsync(string query, int take = 30)
    {
        var types = await GetProductTypesAsync();

        if (types.Length == 0)
        {
            return [];
        }

        var search = query?.Trim();
        var items = string.IsNullOrEmpty(search)
            ? await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(types)).OrderBy(index => index.DisplayText).Take(take).ListAsync()
            : await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(types) && index.DisplayText.Contains(search)).OrderBy(index => index.DisplayText).Take(take).ListAsync();

        return [.. items.Select(item => new CouponItem(item.ContentItemId, item.DisplayText, item.ContentType))];
    }

    /// <summary>
    /// Returns the products with the given ids.
    /// </summary>
    public async Task<IReadOnlyList<CouponItem>> GetProductsAsync(IEnumerable<string> ids)
    {
        var wanted = ids?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (wanted.Length == 0)
        {
            return [];
        }

        var items = await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentItemId.IsIn(wanted)).ListAsync();
        var found = items.ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);

        // A product deleted since keeps its place, so saving the coupon doesn't quietly drop the limit.
        return [.. wanted.Select(id => found.TryGetValue(id, out var item) ? new CouponItem(id, item.DisplayText, item.ContentType) : new CouponItem(id, id, null))];
    }

    /// <summary>
    /// Returns the taxonomy terms whose name contains <paramref name="query"/>, or every term, with the taxonomy each
    /// belongs to.
    /// </summary>
    public async Task<IReadOnlyList<CouponItem>> SearchCategoriesAsync(string query, int take = 50)
    {
        var search = query?.Trim();

        return [.. (await GetTermsAsync())
            .Where(term => string.IsNullOrEmpty(search) || term.Text.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Take(take)];
    }

    /// <summary>
    /// Returns the taxonomy terms with the given ids.
    /// </summary>
    public async Task<IReadOnlyList<CouponItem>> GetCategoriesAsync(IEnumerable<string> ids)
    {
        var wanted = ids?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (wanted.Length == 0)
        {
            return [];
        }

        var terms = (await GetTermsAsync()).ToDictionary(term => term.Id, StringComparer.Ordinal);

        return [.. wanted.Select(id => terms.TryGetValue(id, out var term) ? term : new CouponItem(id, id, null))];
    }

    private async Task<string[]> GetProductTypesAsync()
        => [.. (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name == nameof(ProductPart)))
            .Select(type => type.Name)];

    private async Task<List<CouponItem>> GetTermsAsync()
    {
        var terms = new List<CouponItem>();

        foreach (var taxonomy in await _session.Query<ContentItem, ContentItemIndex>(index => index.Published && index.ContentType == TaxonomyContentType).ListAsync())
        {
            var json = JObject.FromObject(taxonomy);

            Collect(json["TaxonomyPart"]?["Terms"] as JsonArray, taxonomy.DisplayText, terms);
        }

        return [.. terms.OrderBy(term => term.Group, StringComparer.OrdinalIgnoreCase).ThenBy(term => term.Text, StringComparer.OrdinalIgnoreCase)];
    }

    private static void Collect(JsonArray nodes, string taxonomy, List<CouponItem> terms)
    {
        foreach (var node in nodes?.OfType<JsonObject>() ?? [])
        {
            var id = node["ContentItemId"]?.GetValue<string>();
            var text = node["DisplayText"]?.GetValue<string>();

            if (!string.IsNullOrEmpty(id))
            {
                terms.Add(new CouponItem(id, string.IsNullOrEmpty(text) ? id : text, taxonomy));
            }

            Collect(node["Terms"] as JsonArray, taxonomy, terms);
        }
    }
}

/// <summary>
/// A product or category a coupon can be limited to.
/// </summary>
/// <param name="Id">Its content item id.</param>
/// <param name="Text">Its name.</param>
/// <param name="Group">The product's type, or the taxonomy the category belongs to.</param>
public sealed record CouponItem(string Id, string Text, string Group);
