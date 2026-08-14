namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class CreateSubscriptionResponse
{
    public string Id { get; set; }

    public string Status { get; set; }

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
