using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Aggregates the tax-relevant context for a subscription transaction. It reuses the taxation
/// framework's own address, customer, and price-type models rather than introducing a parallel tax
/// abstraction, and is produced by an <see cref="ISubscriptionTaxProfileProvider"/> so that address
/// and customer resolution can be extended or replaced.
/// </summary>
public sealed class SubscriptionTaxProfile
{
    /// <summary>
    /// Gets or sets the merchant/ship-from origin address.
    /// </summary>
    public Address Origin { get; set; }

    /// <summary>
    /// Gets or sets the customer/ship-to destination address.
    /// </summary>
    public Address Destination { get; set; }

    /// <summary>
    /// Gets or sets the customer tax profile (residence, business status, exemptions).
    /// </summary>
    public CustomerTaxProfile Customer { get; set; }

    /// <summary>
    /// Gets or sets the default tax category code applied to line items that do not carry their own.
    /// </summary>
    public string DefaultTaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the default tax classification code applied to line items that do not carry their own.
    /// </summary>
    public string DefaultTaxClassificationCode { get; set; }

    /// <summary>
    /// Gets or sets whether the subscription amounts already include tax.
    /// </summary>
    public TaxPriceType PriceType { get; set; } = TaxPriceType.Exclusive;
}
