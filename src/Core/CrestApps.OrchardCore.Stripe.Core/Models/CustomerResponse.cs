namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents customer details returned from Stripe.
/// </summary>
public class CustomerResponse
{
    /// <summary>
    /// Gets or sets the Stripe customer identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the customer's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the customer's email address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the customer's phone number.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the metadata stored on the customer, such as the checkout session it was created for.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}
