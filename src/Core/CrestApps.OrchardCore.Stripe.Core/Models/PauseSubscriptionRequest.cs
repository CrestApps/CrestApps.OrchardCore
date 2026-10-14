namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a request to suspend or resume collection on a Stripe subscription without ending it.
/// </summary>
public class PauseSubscriptionRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the Stripe subscription identifier.
    /// </summary>
    public string SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether collection is suspended. When <see langword="false"/> the
    /// subscription returns to normal collection.
    /// </summary>
    public bool Paused { get; set; }

    /// <summary>
    /// Gets or sets the reason recorded on the subscription for audit.
    /// </summary>
    public string Reason { get; set; }
}
