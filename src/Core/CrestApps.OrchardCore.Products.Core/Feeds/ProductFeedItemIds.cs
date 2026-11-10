namespace CrestApps.OrchardCore.Products.Core.Feeds;

/// <summary>
/// Makes the id a product, or one of its variants, has in product feeds. Checkouts accept the same id as the item to
/// buy, so an agent that found an item in a feed buys exactly it; <see cref="IProductItemIdResolver"/> reads it back.
/// </summary>
public static class ProductFeedItemIds
{
    /// <summary>
    /// The longest id a feed carries, Google Merchant Center's limit. A longer id is cut to it.
    /// </summary>
    public const int MaxLength = 50;

    /// <summary>
    /// Returns the feed id of a product or variant: its SKU, else the product's id for a product without variants, or
    /// the product's id and the variant's joined by <c>-</c>, cut to <see cref="MaxLength"/> characters.
    /// </summary>
    /// <param name="contentItemId">The content item id of the product.</param>
    /// <param name="variantId">The variant's id, or <see langword="null"/> for a product without variants.</param>
    /// <param name="sku">The SKU of the variant, or of the product without variants, or <see langword="null"/>.</param>
    public static string Create(string contentItemId, string variantId, string sku)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentItemId);

        var id = !string.IsNullOrEmpty(sku)
            ? sku
            : string.IsNullOrEmpty(variantId)
                ? contentItemId
                : $"{contentItemId}-{variantId}";

        return Truncate(id);
    }

    /// <summary>
    /// Returns an id cut to <see cref="MaxLength"/> characters, without trailing spaces.
    /// </summary>
    /// <param name="id">The id.</param>
    public static string Truncate(string id)
        => id is null || id.Length <= MaxLength
            ? id
            : id.Substring(0, MaxLength).TrimEnd();
}
