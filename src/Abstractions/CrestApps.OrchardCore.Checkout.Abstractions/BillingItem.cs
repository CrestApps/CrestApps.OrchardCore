namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// A single amount that a checkout flow step contributes to the checkout invoice. Steps declare their
/// billing items so the framework can build a single authoritative invoice for the whole checkout.
/// </summary>
public sealed class BillingItem
{
    /// <summary>
    /// A stable identifier for the item, used to correlate it back to the thing being purchased.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// A human-readable description shown to the customer.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// The amount to charge. For a recurring item this is the amount charged every billing cycle.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// The recurring plan for this item, or <see langword="null"/> for a one-time charge.
    /// </summary>
    public RecurringPlan Plan { get; set; }
}
