
namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents one Stripe price and quantity pair to include in a subscription.
/// </summary>
public class CreateSubscriptionLineItem
{
    /// <summary>
    /// Gets or sets the number of price units to include in the subscription item.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the Stripe price identifier for the subscription item.
    /// </summary>
    public string PriceId { get; set; }

    /// <summary>
    /// Gets or sets the metadata to store with the subscription item in Stripe.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }

    /// <summary>
    /// Gets or sets the price to define inline when <see cref="PriceId"/> is not supplied.
    /// </summary>
    /// <remarks>
    /// Defining the price inline is what lets a customer subscribe to any product at any price point.
    /// Requiring a pre-created Stripe price would mean every amount had to be synchronized to Stripe before
    /// it could be sold, which makes per-customer and computed pricing impossible.
    /// </remarks>
    public SubscriptionInlinePrice Price { get; set; }
}

/// <summary>
/// A recurring price defined at subscription time rather than looked up from a pre-created Stripe price.
/// </summary>
public class SubscriptionInlinePrice
{
    /// <summary>
    /// Gets or sets the recurring amount for one unit, in major currency units.
    /// </summary>
    public decimal UnitAmount { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency code.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the recurring interval. Valid values are <c>day</c>, <c>week</c>, <c>month</c>, and <c>year</c>.
    /// </summary>
    public string Interval { get; set; }

    /// <summary>
    /// Gets or sets the number of intervals between billings.
    /// </summary>
    public int IntervalCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the Stripe product identifier the price belongs to. When empty, a product is created
    /// from <see cref="ProductName"/>.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Gets or sets the stable key identifying this offer at Stripe.
    /// </summary>
    /// <remarks>
    /// When it is set the price is created once and reused for every later subscription to the same offer,
    /// which keeps the Stripe account to one price per offer instead of one per customer and lets Stripe's
    /// own reporting group by it. It is left empty for an amount the buyer named, which has no reusable
    /// offer behind it.
    /// </remarks>
    public string LookupKey { get; set; }

    /// <summary>
    /// Gets or sets the product name used when no <see cref="ProductId"/> is supplied. It is what the
    /// customer sees on their Stripe invoice, so it should describe what they bought.
    /// </summary>
    public string ProductName { get; set; }
}
