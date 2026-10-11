namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// The default, immutable <see cref="ISellableProduct"/> implementation produced by an
/// <c>IProductSnapshotResolver</c>.
/// </summary>
public sealed class SellableProduct : ISellableProduct
{
    /// <inheritdoc/>
    public string ContentItemId { get; init; }

    /// <inheritdoc/>
    public string ContentItemVersionId { get; init; }

    /// <inheritdoc/>
    public string ContentType { get; init; }

    /// <inheritdoc/>
    public string Sku { get; init; }

    /// <inheritdoc/>
    public string Title { get; init; }

    /// <inheritdoc/>
    public decimal UnitPrice { get; init; }

    /// <inheritdoc/>
    public string Currency { get; init; }

    /// <inheritdoc/>
    public ProductType ProductType { get; init; }

    /// <inheritdoc/>
    public string TaxCategoryCode { get; init; }

    /// <inheritdoc/>
    public string TaxClassificationCode { get; init; }

    /// <inheritdoc/>
    public string ExternalTaxCode { get; init; }
}
