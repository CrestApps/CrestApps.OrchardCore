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

    /// <summary>
    /// Gets or sets the catalog price this amount came from, when it came from one at a fixed amount.
    /// </summary>
    /// <remarks>
    /// A gateway reuses its own price object for the same offer instead of minting one per purchase, and
    /// this is what identifies the offer. It is deliberately left empty for an amount the buyer named,
    /// because there is no reusable offer to point at.
    /// </remarks>
    public string PriceId { get; set; }
}
