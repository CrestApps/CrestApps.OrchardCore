namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Defines Stripe payment amount limits for a currency in major currency units.
/// </summary>
public class StripePaymentLimits
{
    /// <summary>
    /// Gets or sets the minimum payment amount allowed by Stripe.
    /// </summary>
    public decimal? Minimum { get; set; }

    /// <summary>
    /// Gets or sets the maximum payment amount allowed by Stripe.
    /// </summary>
    public decimal? Maximum { get; set; }
}
