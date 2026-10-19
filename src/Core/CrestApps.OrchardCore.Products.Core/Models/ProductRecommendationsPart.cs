using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// The products a merchant recommends with a product: upsells, shown on its page as better or bigger choices, and
/// cross-sells, offered in the cart beside it as things that go with it.
/// </summary>
/// <remarks>
/// It is kept on the product without being attached to its content type: the Product Recommendations feature edits
/// it on the product's own editor.
/// </remarks>
public sealed class ProductRecommendationsPart : ContentPart
{
    /// <summary>
    /// Gets or sets the content item ids of the products shown on this product's page as "You may also like".
    /// </summary>
    public List<string> UpsellIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the content item ids of the products offered in the cart when this product is in it.
    /// </summary>
    public List<string> CrossSellIds { get; set; } = [];
}
