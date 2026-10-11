using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// The input to an <see cref="IProductSnapshotResolver"/>. It is a context object rather than a bare
/// content item so future selling scenarios (a requested currency, quantity, variant, or customer tax
/// context) can be added without breaking the resolver signature.
/// </summary>
public sealed class ProductSnapshotContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProductSnapshotContext"/> class.
    /// </summary>
    /// <param name="contentItem">The product content item to resolve into a sellable snapshot.</param>
    public ProductSnapshotContext(ContentItem contentItem)
    {
        ContentItem = contentItem;
    }

    /// <summary>
    /// Gets the product content item to resolve into a sellable snapshot.
    /// </summary>
    public ContentItem ContentItem { get; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency the caller wants the price expressed in, when applicable.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the quantity the caller intends to purchase. Defaults to one.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the requested stock-keeping unit, used by a future variant-aware resolver to select a
    /// specific variation. Ignored by the default resolver.
    /// </summary>
    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets the requested variant identifier, used by a future variant-aware resolver. Ignored by
    /// the default resolver.
    /// </summary>
    public string VariantId { get; set; }

    /// <summary>
    /// Gets or sets the price the buyer chose, when the product offers more than one.
    /// </summary>
    /// <remarks>
    /// A product with several prices is ambiguous without this. When it is not supplied the product's
    /// default price is used, which is what a link straight to "buy" relies on.
    /// </remarks>
    public string PriceId { get; set; }

    /// <summary>
    /// Gets or sets the amount the buyer named, for a price that lets them name one.
    /// </summary>
    /// <remarks>
    /// It is a request, not an instruction: the resolver refuses it when the price does not allow a custom
    /// amount or the amount falls outside the bounds the price sets.
    /// </remarks>
    public decimal? CustomAmount { get; set; }
}
