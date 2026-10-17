using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Customers.Models;

/// <summary>
/// What the buyer entered on an order's Contact step, kept in the checkout session.
/// </summary>
public sealed class CustomerContactStep
{
    public string Name { get; set; }

    public string Email { get; set; }

    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the address-book entry billed, when a signed-in customer picked one.
    /// </summary>
    public string BillingAddressId { get; set; }

    /// <summary>
    /// Gets or sets the address billed, or <see langword="null"/> when none was given.
    /// </summary>
    public Address BillingAddress { get; set; }

    /// <summary>
    /// Gets or sets whether a new billing address is added to the customer's address book when the order is paid.
    /// </summary>
    public bool SaveBillingAddress { get; set; }
}
