namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Describes an item that is billed as part of a subscription flow.
/// </summary>
public class BillingItem
{
    /// <summary>
    /// Gets or sets the description shown for the billed item.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the billed item or plan.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the amount to collect for each billing cycle.
    /// </summary>
    public decimal BillingAmount { get; set; }

    /// <summary>
    /// Gets or sets the subscription plan that defines the recurring billing schedule for the item.
    /// </summary>
    public SubscriptionPlan Subscription { get; set; }
}
