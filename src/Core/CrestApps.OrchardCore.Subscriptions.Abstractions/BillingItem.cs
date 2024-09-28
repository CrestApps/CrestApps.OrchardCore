namespace CrestApps.OrchardCore.Subscriptions;

public class BillingItem
{
    /// <summary>
    /// Item description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// The plan identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// The billing amount to collect every billing cycle.
    /// </summary>
    public double BillingAmount { get; set; }

    public SubscriptionPlan Subscription { get; set; }
}
