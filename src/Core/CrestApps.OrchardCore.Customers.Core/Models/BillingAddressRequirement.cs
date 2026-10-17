namespace CrestApps.OrchardCore.Customers.Core.Models;

/// <summary>
/// Whether an order's checkout asks for a billing address. Optional comes first, so it is the default.
/// </summary>
public enum BillingAddressRequirement
{
    /// <summary>
    /// The checkout asks for the billing address but lets the buyer leave it blank.
    /// </summary>
    Optional,

    /// <summary>
    /// The checkout does not ask.
    /// </summary>
    Off,

    /// <summary>
    /// Every order needs a billing address, for example for invoices or tax.
    /// </summary>
    Required,
}
