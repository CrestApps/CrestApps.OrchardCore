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
}
