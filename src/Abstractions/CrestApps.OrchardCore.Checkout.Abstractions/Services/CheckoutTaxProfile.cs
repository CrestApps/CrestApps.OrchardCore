using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Aggregates the tax-relevant context for a checkout transaction. It reuses the taxation framework's
/// own address, customer, and price-type models rather than introducing a parallel abstraction, and is
/// produced by an <see cref="ICheckoutTaxProfileProvider"/> so address and customer resolution can be
/// extended or replaced.
/// </summary>
public sealed class CheckoutTaxProfile
{
    /// <summary>
    /// The merchant/ship-from origin address.
    /// </summary>
    public TaxAddress Origin { get; set; }

    /// <summary>
    /// The customer/ship-to destination address.
    /// </summary>
    public TaxAddress Destination { get; set; }

    /// <summary>
    /// The customer tax profile (residence, business status, exemptions).
    /// </summary>
    public CustomerTaxProfile Customer { get; set; }

    /// <summary>
    /// The default tax category code applied to line items that do not carry their own.
    /// </summary>
    public string DefaultTaxCategoryCode { get; set; }

    /// <summary>
    /// The default tax classification code applied to line items that do not carry their own.
    /// </summary>
    public string DefaultTaxClassificationCode { get; set; }

    /// <summary>
    /// Whether the checkout amounts already include tax.
    /// </summary>
    public TaxPriceType PriceType { get; set; } = TaxPriceType.Exclusive;
}
