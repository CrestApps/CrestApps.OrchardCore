using CrestApps.OrchardCore.Taxation.Models;

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
    /// The amount to charge for one unit. For a recurring item this is the amount charged every billing cycle.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets how many units are bought. The item charges <see cref="Amount"/> for each.
    /// </summary>
    public int Quantity { get; set; } = 1;

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

    /// <summary>
    /// Gets or sets a value indicating whether the amount is left out of the tax determination. Set it for an
    /// amount whose tax was already decided elsewhere, such as a balance being settled that already carries its
    /// own tax; taxing it again would charge the tax twice.
    /// </summary>
    public bool ExcludeFromTax { get; set; }

    /// <summary>
    /// Gets or sets whether no discount takes anything off the item, such as one at a price staff agreed on a quote.
    /// </summary>
    public bool ExcludeFromDiscounts { get; set; }

    /// <summary>
    /// Gets or sets the stock keeping unit of the product the item is for, when it is for one.
    /// </summary>
    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets the item's tax category, such as the product's. The checkout's default category is used
    /// when it is empty.
    /// </summary>
    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the item's tax classification. The checkout's default classification is used when it is
    /// empty.
    /// </summary>
    public string TaxClassificationCode { get; set; }

    /// <summary>
    /// Gets or sets what kind of thing the item is for tax purposes. When it is empty, a one-time item is taxed as
    /// a physical good and a recurring one as a service.
    /// </summary>
    public TaxableItemKind? TaxableKind { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the product the item is for, such as its content item id, so a discount
    /// limited to some products can find it.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Gets or sets the categories the product is in, such as its taxonomy term ids, so a discount limited to some
    /// categories can find it.
    /// </summary>
    public string[] CategoryIds { get; set; }
}
