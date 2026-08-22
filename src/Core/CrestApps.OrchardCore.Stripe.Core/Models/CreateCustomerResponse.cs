namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents the result returned after creating a Stripe customer.
/// </summary>
public class CreateCustomerResponse
{
    /// <summary>
    /// Gets or sets the Stripe customer identifier.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the customer name stored in Stripe.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the customer phone number stored in Stripe.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the customer email address stored in Stripe.
    /// </summary>
    public string Email { get; set; }
}
