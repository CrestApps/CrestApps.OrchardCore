using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// A way to ship that a zone offers, such as <em>Standard</em> or <em>Express</em>, priced by a rate provider. The
/// provider's own settings are kept in the method's properties.
/// </summary>
public sealed class ShippingMethod : Entity
{
    public string MethodId { get; set; }

    /// <summary>
    /// Gets or sets the name buyers see, such as <em>Standard</em>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what buyers see under the name, such as <em>3 to 5 business days</em>.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the name of the <c>IShippingRateProvider</c> that prices the method.
    /// </summary>
    public string ProviderName { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the method's place in the zone's list, smallest first.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the currency the method's amounts are in. A method only offers rates for orders in its currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the tax category of the shipping charge, or <see langword="null"/> for the store's default.
    /// </summary>
    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets what each unit of a product in a shipping class (the key) adds to the method's rate.
    /// </summary>
    public Dictionary<string, decimal> ClassSurcharges { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
