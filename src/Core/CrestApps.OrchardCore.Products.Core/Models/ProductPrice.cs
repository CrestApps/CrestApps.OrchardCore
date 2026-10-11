namespace CrestApps.OrchardCore.Products.Core.Models;

/// <summary>
/// One way a product may be bought: an amount in a currency, charged once or on a schedule.
/// </summary>
/// <remarks>
/// A product can carry several of these — a monthly plan, an annual plan at a discount, a one-time
/// purchase, a pay-what-you-want tier — and the buyer picks one at checkout. Everything that decides what
/// the gateway is told lives here rather than on the product, so selling the same product on new terms is
/// adding a price, not changing a schema.
/// </remarks>
public sealed class ProductPrice
{
    /// <summary>
    /// Gets or sets the stable identifier the checkout and the gateway correlate on.
    /// </summary>
    /// <remarks>
    /// It has to survive editing: a buyer who is shown a price, leaves, and comes back must be charged the
    /// price they chose, and a gateway price is reused by this id rather than recreated per purchase.
    /// </remarks>
    public string PriceId { get; set; }

    /// <summary>
    /// Gets or sets the name shown to the buyer, such as "Monthly" or "Annual".
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency the amount is expressed in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the amount, in major currency units.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets whether the amount is charged once or repeats.
    /// </summary>
    public PriceKind Kind { get; set; }

    /// <summary>
    /// Gets or sets how many <see cref="BillingInterval"/> units make one billing cycle.
    /// </summary>
    public int? BillingDuration { get; set; }

    /// <summary>
    /// Gets or sets the unit the billing cycle is measured in.
    /// </summary>
    public BillingInterval? Interval { get; set; }

    /// <summary>
    /// Gets or sets how many cycles are billed before the agreement ends on its own. Null bills until the
    /// customer cancels.
    /// </summary>
    public int? BillingCycleLimit { get; set; }

    /// <summary>
    /// Gets or sets how many days pass before the first cycle is billed.
    /// </summary>
    public int? StartDayDelay { get; set; }

    /// <summary>
    /// Gets or sets how many days the buyer has before the first cycle is charged.
    /// </summary>
    public int? TrialDays { get; set; }

    /// <summary>
    /// Gets or sets a one-time amount charged alongside the first cycle.
    /// </summary>
    public decimal? SetupFee { get; set; }

    /// <summary>
    /// Gets or sets what the setup fee is called on the invoice.
    /// </summary>
    public string SetupFeeDescription { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the buyer names the amount.
    /// </summary>
    public bool AllowCustomAmount { get; set; }

    /// <summary>
    /// Gets or sets the least the buyer may name when <see cref="AllowCustomAmount"/> is set.
    /// </summary>
    public decimal? MinimumAmount { get; set; }

    /// <summary>
    /// Gets or sets the most the buyer may name when <see cref="AllowCustomAmount"/> is set.
    /// </summary>
    public decimal? MaximumAmount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the buyer may buy more than one.
    /// </summary>
    public bool AllowQuantity { get; set; }

    /// <summary>
    /// Gets or sets the most the buyer may take when <see cref="AllowQuantity"/> is set.
    /// </summary>
    public int? MaximumQuantity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is the price offered when the buyer names none.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the price may be bought at all.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets when the price starts being offered.
    /// </summary>
    public DateTime? EffectiveFromUtc { get; set; }

    /// <summary>
    /// Gets or sets when the price stops being offered.
    /// </summary>
    public DateTime? EffectiveToUtc { get; set; }

    /// <summary>
    /// Whether the price may be bought at the supplied moment.
    /// </summary>
    /// <param name="utcNow">The current UTC time.</param>
    /// <remarks>
    /// A price that has been withdrawn is not deleted, because an agreement created from it still names it.
    /// This is what decides whether it may be bought *again*, never what an existing agreement bills.
    /// </remarks>
    public bool IsAvailable(DateTime utcNow)
        => IsActive &&
           (!EffectiveFromUtc.HasValue || EffectiveFromUtc.Value <= utcNow) &&
           (!EffectiveToUtc.HasValue || EffectiveToUtc.Value > utcNow);

    /// <summary>
    /// The amount to charge for this price, given what the buyer named.
    /// </summary>
    /// <param name="customAmount">The amount the buyer named, when the price allows one.</param>
    /// <returns>The amount to charge, or <see langword="null"/> when what the buyer named is not allowed.</returns>
    /// <remarks>
    /// The bounds are enforced here rather than in the editor alone, because the amount arrives from the
    /// browser: a price that says "pay what you want, at least 5" must not be charged 0 because somebody
    /// edited the form.
    /// </remarks>
    public decimal? GetChargeableAmount(decimal? customAmount)
    {
        if (!AllowCustomAmount || !customAmount.HasValue)
        {
            return Amount;
        }

        var amount = customAmount.Value;

        if (amount <= 0m ||
            (MinimumAmount.HasValue && amount < MinimumAmount.Value) ||
            (MaximumAmount.HasValue && amount > MaximumAmount.Value))
        {
            return null;
        }

        return amount;
    }

    /// <summary>
    /// The quantity to charge for, given what the buyer asked for.
    /// </summary>
    /// <param name="quantity">The quantity the buyer asked for.</param>
    /// <returns>The quantity to charge for, or <see langword="null"/> when it is more than the price allows.</returns>
    public int? GetChargeableQuantity(int quantity)
    {
        if (quantity < 1)
        {
            quantity = 1;
        }

        if (!AllowQuantity)
        {
            return quantity == 1 ? 1 : null;
        }

        if (MaximumQuantity.HasValue && quantity > MaximumQuantity.Value)
        {
            return null;
        }

        return quantity;
    }
}
