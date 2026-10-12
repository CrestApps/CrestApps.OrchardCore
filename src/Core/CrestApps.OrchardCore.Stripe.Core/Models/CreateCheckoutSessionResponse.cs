namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents the result returned after creating a Stripe Checkout Session.
/// </summary>
public sealed class CreateCheckoutSessionResponse
{
    /// <summary>
    /// Gets or sets the Stripe Checkout Session identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the absolute URL the browser should be redirected to for a hosted checkout.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the client secret used to render an embedded checkout.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the Stripe status of the Checkout Session.
    /// </summary>
    public string Status { get; set; }
}
