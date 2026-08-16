namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// Represents a request to create a Stripe Checkout session for a subscription session.
/// </summary>
public sealed class CreateSessionCheckout
{
    /// <summary>
    /// Gets or sets the subscription session identifier.
    /// </summary>
    public string SessionId { get; set; }
}
