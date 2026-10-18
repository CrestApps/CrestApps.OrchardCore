namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// The reserved keys of the attributes every product has without a field.
/// </summary>
/// <remarks>
/// They contain a dot, which a content field name cannot, so they never collide with a field's own key.
/// </remarks>
public static class ProductAttributeKeys
{
    /// <summary>
    /// The price.
    /// </summary>
    public const string Price = "Product.Price";

    /// <summary>
    /// The content type.
    /// </summary>
    public const string ContentType = "Product.ContentType";

    /// <summary>
    /// Whether the product can be bought online.
    /// </summary>
    public const string Purchasable = "Product.Purchasable";

    /// <summary>
    /// The title.
    /// </summary>
    public const string Title = "Product.Title";

    /// <summary>
    /// When the product was published.
    /// </summary>
    public const string Published = "Product.Published";

    /// <summary>
    /// A product's place in the order its merchant arranged. A smaller number comes first; the "Recommended" sort
    /// uses it.
    /// </summary>
    public const string Position = "Product.Position";

    /// <summary>
    /// Whether the merchant features the product.
    /// </summary>
    public const string Featured = "Product.Featured";

    /// <summary>
    /// Whether some of the product, or of one of its variants, is in stock. Written by a module that counts stock,
    /// such as Inventory; without one, products have no value and the "In stock" filter doesn't show.
    /// </summary>
    public const string InStock = "Product.InStock";

    /// <summary>
    /// The product's average customer rating, from 1 to 5. Written by a module that keeps reviews, such as Product
    /// Reviews; without one, products have no value.
    /// </summary>
    public const string Rating = "Product.Rating";

    /// <summary>
    /// How many customer reviews count toward <see cref="Rating"/>.
    /// </summary>
    public const string ReviewCount = "Product.ReviewCount";

    /// <summary>
    /// Whether the key names a built-in attribute rather than a field.
    /// </summary>
    /// <param name="key">The attribute key.</param>
    public static bool IsBuiltIn(string key)
        => key is not null && key.StartsWith("Product.", StringComparison.Ordinal);
}
