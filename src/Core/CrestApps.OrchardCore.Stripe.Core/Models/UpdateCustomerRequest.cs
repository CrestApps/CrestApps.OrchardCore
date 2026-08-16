namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents the customer values to update in Stripe.
/// </summary>
public class UpdateCustomerRequest
{
    /// <summary>
    /// Gets or sets the updated customer name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the updated customer email address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the updated customer phone number.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the metadata to store with the customer in Stripe.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}
