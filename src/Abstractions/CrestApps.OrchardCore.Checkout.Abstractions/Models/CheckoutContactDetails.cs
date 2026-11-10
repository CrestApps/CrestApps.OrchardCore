using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// Who the buyer of a checkout is, as code gives it.
/// </summary>
public sealed class CheckoutContactDetails
{
    /// <summary>
    /// Gets or sets the buyer's full name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the buyer's e-mail address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the buyer's phone number.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the address to bill, or <see langword="null"/>.
    /// </summary>
    public Address BillingAddress { get; set; }
}
