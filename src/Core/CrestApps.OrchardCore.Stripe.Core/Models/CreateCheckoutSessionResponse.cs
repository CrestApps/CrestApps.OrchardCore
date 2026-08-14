namespace CrestApps.OrchardCore.Stripe.Core.Models;

public sealed class CreateCheckoutSessionResponse
{
    public string Id { get; set; }

    /// <summary>
    /// The absolute URL the browser should be redirected to for a hosted checkout.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// The client secret used to render an embedded checkout.
    /// </summary>
    public string ClientSecret { get; set; }

    public string Status { get; set; }
}
