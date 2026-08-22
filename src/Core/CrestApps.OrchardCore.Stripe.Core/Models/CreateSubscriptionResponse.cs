namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents the result returned after a Stripe subscription is created.
/// </summary>
public class CreateSubscriptionResponse
{
    /// <summary>
    /// Gets or sets the Stripe subscription identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the current Stripe status of the subscription.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the client secret for confirming the subscription's initial invoice payment.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Indicates that the subscription's initial invoice could not be paid without additional
    /// customer action (for example 3-D Secure / SCA authentication). Stripe reports the
    /// subscription as <c>incomplete</c> in this case and exposes a client secret that the
    /// browser must confirm to finalize the first payment.
    /// </summary>
    public bool RequiresAction
        => string.Equals(Status, "incomplete", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(ClientSecret);
}
