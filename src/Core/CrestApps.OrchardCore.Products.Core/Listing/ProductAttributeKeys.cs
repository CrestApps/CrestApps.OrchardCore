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
    /// Whether the key names a built-in attribute rather than a field.
    /// </summary>
    /// <param name="key">The attribute key.</param>
    public static bool IsBuiltIn(string key)
        => key is not null && key.StartsWith("Product.", StringComparison.Ordinal);
}
