using CrestApps.OrchardCore.Products.Core.Feeds;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Resolves agents' item ids with the same rule the product feeds give them out with: a SKU, a product's content item
/// id, or its id and a variant's id joined by a dash.
/// </summary>
public sealed class ProductAgentItemResolver : IAgentItemResolver
{
    private readonly IProductItemIdResolver _resolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductAgentItemResolver"/> class.
    /// </summary>
    /// <param name="resolver">The product feeds' item id resolver.</param>
    public ProductAgentItemResolver(IProductItemIdResolver resolver)
    {
        _resolver = resolver;
    }

    /// <inheritdoc />
    public async Task<AgentItem> ResolveAsync(string itemId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        var reference = await _resolver.ResolveAsync(itemId.Trim(), cancellationToken);

        return reference is null ? null : new AgentItem(reference.ItemId ?? itemId.Trim(), reference.ContentItemId, reference.VariantId);
    }

    /// <inheritdoc />
    public string GetItemId(string contentItemId, string variantId, string sku)
        => ProductFeedItemIds.Create(contentItemId, variantId, sku);
}
