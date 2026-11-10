namespace CrestApps.OrchardCore.Products.Core.Feeds;

/// <summary>
/// Reads back the item id a product feed gives a product or variant (see <see cref="ProductFeedItemIds"/>), such as
/// the id an AI agent's checkout names the item to buy by.
/// </summary>
public interface IProductItemIdResolver
{
    /// <summary>
    /// Returns the published product, or variant of one, that can be bought and that the id names: a product's or
    /// variant's SKU, a product's content item id, or a product's content item id and a variant's id joined by
    /// <c>-</c>. Returns <see langword="null"/> when none does.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="cancellationToken">A token that cancels the work.</param>
    Task<ProductItemReference> ResolveAsync(string itemId, CancellationToken cancellationToken = default);
}
