using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Where and how the order is shipped, as chosen at checkout. The tax destination is the shipping address, and the
/// order keeps both once it is paid.
/// </summary>
public sealed class CheckoutShippingInfo
{
    public Address Address { get; set; }

    /// <summary>
    /// Gets or sets the id of the rate chosen: the shipping method's id, then the provider's option when it has several.
    /// </summary>
    public string RateId { get; set; }

    public string MethodId { get; set; }

    public string ZoneId { get; set; }

    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the method's name, such as <em>Standard</em>.
    /// </summary>
    public string Name { get; set; }

    public string Description { get; set; }

    /// <summary>
    /// Gets or sets what shipping costs, before tax.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the location the order is fulfilled from, such as the shop it's picked up at, or
    /// <see langword="null"/>.
    /// </summary>
    public string LocationId { get; set; }
}
