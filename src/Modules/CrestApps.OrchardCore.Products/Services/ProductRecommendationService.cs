using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Indexes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.ContentManagement.Metadata;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Picks the products to recommend: a product's upsells and cross-sells as the merchant chose them, and related
/// products that share its most specific categories.
/// </summary>
public sealed class ProductRecommendationService
{
    /// <summary>
    /// The most products a recommendation strip shows.
    /// </summary>
    public const int MaxItems = 8;

    private readonly ISession _session;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ProductAttributeCatalog _attributeCatalog;

    public ProductRecommendationService(ISession session, IContentDefinitionManager contentDefinitionManager, ProductAttributeCatalog attributeCatalog)
    {
        _session = session;
        _contentDefinitionManager = contentDefinitionManager;
        _attributeCatalog = attributeCatalog;
    }

    /// <summary>
    /// Returns the products related to this one: those sharing the most of its deepest categories, most shared first,
    /// then featured, leaving out the product itself and the ones excluded.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetRelatedIdsAsync(string contentItemId, IEnumerable<string> exclude, int take = 4)
    {
        if (string.IsNullOrEmpty(contentItemId))
        {
            return [];
        }

        var document = await _session.Query<ProductSearchDocument, ProductSearchIndex>(index => index.ContentItemId == contentItemId).FirstOrDefaultAsync();

        if (document is null)
        {
            return [];
        }

        // The deepest term of each category field: "Running shoes" says more about a product than "Footwear".
        var categoryKeys = (await _attributeCatalog.GetAttributesAsync([document.ContentType]))
            .Where(attribute => attribute.Kind == ProductAttributeValueKind.Term)
            .Select(attribute => attribute.Key)
            .ToHashSet(StringComparer.Ordinal);

        var terms = (document.Attributes ?? [])
            .Where(value => categoryKeys.Contains(value.Key) && !string.IsNullOrEmpty(value.Text))
            .GroupBy(value => value.Key, StringComparer.Ordinal)
            .SelectMany(group => group.Where(value => value.Level == group.Max(entry => entry.Level)))
            .ToList();

        if (terms.Count == 0)
        {
            return [];
        }

        var excluded = new HashSet<string>(exclude ?? [], StringComparer.Ordinal) { contentItemId };
        var scores = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var term in terms)
        {
            var key = term.Key;
            var text = term.Text;
            var matches = await _session.QueryIndex<ProductAttributeIndex>(index => index.AttributeKey == key && index.TextValue == text).Take(200).ListAsync();

            foreach (var match in matches)
            {
                if (!excluded.Contains(match.ContentItemId))
                {
                    scores[match.ContentItemId] = scores.GetValueOrDefault(match.ContentItemId) + 1;
                }
            }
        }

        if (scores.Count == 0)
        {
            return [];
        }

        var candidates = scores.Keys.ToArray();
        var featured = (await _session.QueryIndex<ProductSearchIndex>(index => index.ContentItemId.IsIn(candidates) && index.Featured).ListAsync())
            .Select(index => index.ContentItemId)
            .ToHashSet(StringComparer.Ordinal);

        return [.. scores
            .OrderByDescending(pair => pair.Value)
            .ThenByDescending(pair => featured.Contains(pair.Key))
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key)
            .Take(Math.Clamp(take, 1, MaxItems))];
    }

    /// <summary>
    /// Returns what the cart's products are sold with: their cross-sells, the most recommended first, leaving out what
    /// is already in the cart.
    /// </summary>
    public static IReadOnlyList<string> GetCrossSellIds(IEnumerable<ContentItem> cartProducts, int take = 4)
    {
        var products = cartProducts?.Where(item => item is not null).ToList() ?? [];
        var inCart = products.Select(item => item.ContentItemId).ToHashSet(StringComparer.Ordinal);
        var votes = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var item in products)
        {
            if (!item.TryGet<ProductRecommendationsPart>(out var part))
            {
                continue;
            }

            foreach (var id in part.CrossSellIds ?? [])
            {
                if (string.IsNullOrEmpty(id) || inCart.Contains(id))
                {
                    continue;
                }

                if (!votes.ContainsKey(id))
                {
                    order.Add(id);
                }

                votes[id] = votes.GetValueOrDefault(id) + 1;
            }
        }

        return [.. order.OrderByDescending(id => votes[id]).Take(Math.Clamp(take, 1, MaxItems))];
    }

    /// <summary>
    /// Searches the products by name, for the merchant's pickers.
    /// </summary>
    public async Task<IReadOnlyList<(string Id, string Text, string Type)>> SearchAsync(string query, int take = 30)
    {
        var types = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name == nameof(ProductPart)))
            .Select(type => type.Name)
            .ToArray();

        if (types.Length == 0)
        {
            return [];
        }

        var search = query?.Trim();
        var items = string.IsNullOrEmpty(search)
            ? await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(types)).OrderBy(index => index.DisplayText).Take(take).ListAsync()
            : await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType.IsIn(types) && index.DisplayText.Contains(search)).OrderBy(index => index.DisplayText).Take(take).ListAsync();

        return [.. items.Select(item => (item.ContentItemId, item.DisplayText, item.ContentType))];
    }

    /// <summary>
    /// Returns the names of the products, in the order given; one deleted since keeps its id as its name.
    /// </summary>
    public async Task<IReadOnlyList<(string Id, string Text)>> NameAsync(IEnumerable<string> ids)
    {
        var wanted = ids?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (wanted.Length == 0)
        {
            return [];
        }

        var found = (await _session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentItemId.IsIn(wanted)).ListAsync())
            .ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);

        return [.. wanted.Select(id => (id, found.TryGetValue(id, out var item) ? item.DisplayText : id))];
    }
}
