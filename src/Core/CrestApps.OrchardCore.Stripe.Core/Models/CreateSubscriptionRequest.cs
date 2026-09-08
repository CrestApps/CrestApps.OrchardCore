using CrestApps.OrchardCore.Payments.Models;

namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents the data required to create a Stripe subscription.
/// </summary>
public class CreateSubscriptionRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the Stripe customer identifier that owns the subscription.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the Stripe payment method identifier to use as the subscription's default payment method.
    /// </summary>
    public string PaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets the metadata to store with the subscription in Stripe.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }

    /// <summary>
    /// Gets or sets the subscription line items to create from Stripe prices.
    /// </summary>
    public IList<CreateSubscriptionLineItem> LineItems { get; set; }

    /// <summary>
    /// Gets or sets the number of trial duration units to apply before billing starts.
    /// </summary>
    public int? TrialDuration { get; set; }

    /// <summary>
    /// Gets or sets the number of billing cycles after which the subscription schedule should cancel.
    /// </summary>
    public int? BillingCycles { get; set; }

    /// <summary>
    /// Gets or sets the unit used to calculate the subscription trial end date.
    /// </summary>
    public DurationType TrialDurationType { get; set; }

    /// <summary>
    /// Gets or sets an amount taken off the first cycle only, in the subscription's currency. It is applied
    /// at Stripe as a single-use coupon so the agreement itself keeps its full recurring price.
    /// </summary>
    public decimal? FirstCycleDiscount { get; set; }

    /// <summary>
    /// Gets or sets the number of days before the first cycle is billed, used to place the end of a
    /// cycle-limited agreement after the deferral rather than before it.
    /// </summary>
    public int? DeferralDays { get; set; }
}
