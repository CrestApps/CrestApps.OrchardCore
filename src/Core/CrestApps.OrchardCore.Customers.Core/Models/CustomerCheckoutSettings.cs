namespace CrestApps.OrchardCore.Customers.Core.Models;

/// <summary>
/// What an order's checkout asks the buyer, under Settings › Commerce › Customers.
/// </summary>
public sealed class CustomerCheckoutSettings
{
    /// <summary>
    /// Gets or sets whether the checkout asks for a billing address. A checkout keeps the setting it started with.
    /// </summary>
    public BillingAddressRequirement BillingAddress { get; set; } = BillingAddressRequirement.Optional;
}
