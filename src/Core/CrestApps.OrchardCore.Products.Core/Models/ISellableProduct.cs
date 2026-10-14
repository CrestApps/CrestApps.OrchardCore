namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// An immutable, provider-neutral snapshot of a sellable product. It exposes only the identity, price,
/// and tax-classification information a checkout or payment flow needs, so those flows never reach into a
/// product content item's editor internals. A future commerce or ordering module can resolve this
/// snapshot once and persist it on an order line, keeping the historical sale correct even after the
/// underlying product price or definition changes.
/// </summary>
public interface ISellableProduct
{
    /// <summary>
    /// Gets the identifier of the product content item.
    /// </summary>
    string ContentItemId { get; }

    /// <summary>
    /// Gets the version identifier of the product content item captured at resolution time.
    /// </summary>
    string ContentItemVersionId { get; }

    /// <summary>
    /// Gets the content type of the product.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Gets the stock-keeping unit, when the product defines one.
    /// </summary>
    string Sku { get; }

    /// <summary>
    /// Gets the display title of the product.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Gets the unit price expressed in major currency units.
    /// </summary>
    decimal UnitPrice { get; }

    /// <summary>
    /// Gets the ISO-4217 currency code the <see cref="UnitPrice"/> is expressed in, when known.
    /// </summary>
    string Currency { get; }

    /// <summary>
    /// Gets the product type (physical good, service, or digital).
    /// </summary>
    ProductType ProductType { get; }

    /// <summary>
    /// Gets the tax category code resolved for the product, when the Taxation feature classifies it.
    /// </summary>
    string TaxCategoryCode { get; }

    /// <summary>
    /// Gets the tax classification code resolved for the product, when the Taxation feature classifies it.
    /// </summary>
    string TaxClassificationCode { get; }

    /// <summary>
    /// Gets the external tax code resolved for the product, when the Taxation feature provides one.
    /// </summary>
    string ExternalTaxCode { get; }
}
