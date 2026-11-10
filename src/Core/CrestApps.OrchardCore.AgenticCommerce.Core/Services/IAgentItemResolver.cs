namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Turns the item ids agents copy from the store's product feeds into products, and back.
/// </summary>
public interface IAgentItemResolver
{
    /// <summary>
    /// Returns the product and variant an item id names, or <see langword="null"/> when it names nothing for sale.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<AgentItem> ResolveAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the item id of a product or one of its variants, as the product feeds give it.
    /// </summary>
    /// <param name="contentItemId">The product's content item id.</param>
    /// <param name="variantId">The variant's id, or <see langword="null"/>.</param>
    /// <param name="sku">The SKU, or <see langword="null"/>.</param>
    string GetItemId(string contentItemId, string variantId, string sku);
}
