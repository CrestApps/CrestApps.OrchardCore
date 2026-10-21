using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// Says when a product can't be sold to the current shopper, such as outside the markets it's sold in. The add to cart
/// button, the cart and the catalog API ask before selling it.
/// </summary>
public interface IProductSaleRestriction
{
    /// <summary>
    /// Returns why the product can't be sold to the current shopper, or <see langword="null"/> when it can.
    /// </summary>
    ValueTask<string> GetRestrictionAsync(ContentItem product, CancellationToken cancellationToken = default);
}

public static class ProductSaleRestrictionExtensions
{
    /// <summary>
    /// Returns the first reason any restriction gives that the product can't be sold to the current shopper, or
    /// <see langword="null"/>.
    /// </summary>
    public static async ValueTask<string> GetRestrictionAsync(this IEnumerable<IProductSaleRestriction> restrictions, ContentItem product, CancellationToken cancellationToken = default)
    {
        if (product is null)
        {
            return null;
        }

        foreach (var restriction in restrictions ?? [])
        {
            if (await restriction.GetRestrictionAsync(product, cancellationToken) is { Length: > 0 } reason)
            {
                return reason;
            }
        }

        return null;
    }
}
