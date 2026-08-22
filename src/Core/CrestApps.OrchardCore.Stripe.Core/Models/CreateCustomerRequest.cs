namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a request to create a Stripe customer.
/// </summary>
public class CreateCustomerRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the Stripe payment method identifier to make the customer's default payment method.
    /// </summary>
    public string PaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets the customer name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the customer email address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the customer phone number.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the metadata to store on the Stripe customer.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}
