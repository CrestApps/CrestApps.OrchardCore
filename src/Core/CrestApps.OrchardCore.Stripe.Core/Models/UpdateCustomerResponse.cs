namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents the result returned after a Stripe customer update is attempted.
/// </summary>
public class UpdateCustomerResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether Stripe updated the customer successfully.
    /// </summary>
    public bool Updated { get; set; }

    /// <summary>
    /// Gets or sets the Stripe customer identifier.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the customer's name returned from Stripe.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the customer's phone number returned from Stripe.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the customer's email address returned from Stripe.
    /// </summary>
    public string Email { get; set; }
}
