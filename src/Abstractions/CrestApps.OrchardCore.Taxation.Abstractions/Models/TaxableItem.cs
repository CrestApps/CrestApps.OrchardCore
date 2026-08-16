using System.Collections.Generic;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// A default, mutable <see cref="ITaxableItem"/> implementation that taxable-item providers can build
/// and return.
/// </summary>
public sealed class TaxableItem : ITaxableItem
{
    /// <inheritdoc />
    public string Id { get; set; }

    /// <inheritdoc />
    public TaxableItemKind Kind { get; set; } = TaxableItemKind.Physical;

    /// <inheritdoc />
    public decimal Quantity { get; set; } = 1m;

    /// <inheritdoc />
    public decimal UnitPrice { get; set; }

    /// <inheritdoc />
    public decimal DiscountAmount { get; set; }

    /// <inheritdoc />
    public string Currency { get; set; }

    /// <inheritdoc />
    public string TaxCategoryCode { get; set; }

    /// <inheritdoc />
    public string TaxClassificationCode { get; set; }

    /// <inheritdoc />
    public string ExternalTaxCode { get; set; }

    /// <inheritdoc />
    public bool? PriceIncludesTax { get; set; }

    /// <inheritdoc />
    public decimal? Weight { get; set; }

    /// <inheritdoc />
    public decimal? Volume { get; set; }

    /// <inheritdoc />
    public Address Origin { get; set; }

    /// <summary>
    /// Gets or sets the mutable metadata dictionary for the item.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    IReadOnlyDictionary<string, string> ITaxableItem.Metadata => Metadata;
}
