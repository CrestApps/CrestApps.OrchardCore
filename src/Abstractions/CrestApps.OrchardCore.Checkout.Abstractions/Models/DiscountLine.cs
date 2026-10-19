namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// One reduction applied to a checkout invoice, recorded so it can be shown to the customer, reported on,
/// and reproduced on a receipt.
/// </summary>
/// <remarks>
/// The amount is stored rather than the rule that produced it. A percentage recalculated later against a
/// price that has since changed gives a different number than the customer was actually charged, and a
/// receipt that disagrees with the payment is worse than no receipt.
/// </remarks>
public sealed class DiscountLine
{
    /// <summary>
    /// Gets or sets the key of the provider that contributed the discount.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the rule that produced it, for example a coupon code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the description the customer sees.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the amount taken off, in major currency units. It is always positive; the invoice
    /// subtracts it.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets what the discount applies to.
    /// </summary>
    public DiscountTarget Target { get; set; }

    /// <summary>
    /// Gets or sets the invoice lines (<see cref="CheckoutLineItem.ItemId"/>) the discount is limited to, such as the
    /// products a coupon is for. When it is empty, the discount applies to every line its target can discount.
    /// </summary>
    public IList<string> LineItemIds { get; set; }

    /// <summary>
    /// Gets or sets the kind of discount it is, for the rules about which discounts can be used together; when it's
    /// <see langword="null"/>, it's worked out from <see cref="Target"/> and <see cref="LineItemIds"/>.
    /// </summary>
    public DiscountClass? Class { get; set; }

    /// <summary>
    /// Gets or sets the kinds of discount it can be used with. A discount from a provider that doesn't say combines
    /// with every kind, as all discounts did before there were combining rules.
    /// </summary>
    public DiscountCombination CombinesWith { get; set; } = DiscountCombination.All;

    /// <summary>
    /// Gets, for a discount left out because it can't be combined, the description of the discount it gave way to.
    /// </summary>
    public string GaveWayTo { get; set; }

    /// <summary>
    /// Gets or sets whether the discount applied by itself, without the buyer entering a code. Its
    /// <see cref="Code"/> then only identifies where it came from, and the buyer is shown its
    /// <see cref="Description"/>.
    /// </summary>
    public bool IsAutomatic { get; set; }

    /// <summary>
    /// Gets or sets, for a discount that takes more off some lines than others (such as the free item of a buy X get
    /// Y offer), how much it takes off each, by line item id. When it's empty, what the discount takes off is spread
    /// across its lines in proportion to their totals.
    /// </summary>
    public IDictionary<string, decimal> LineShares { get; set; }

    /// <summary>
    /// Gets the kind of discount it is.
    /// </summary>
    public DiscountClass GetClass()
        => Class ?? (Target == DiscountTarget.Shipping ? DiscountClass.Shipping : LineItemIds is { Count: > 0 } ? DiscountClass.Product : DiscountClass.Order);

    /// <summary>
    /// Returns whether the two discounts can be used together: each has to allow the other's kind.
    /// </summary>
    public bool CanCombineWith(DiscountLine other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return CombinesWith.HasFlag(other.GetClass().ToCombination()) && other.CombinesWith.HasFlag(GetClass().ToCombination());
    }

    /// <summary>
    /// Returns whether the discount may take something off the line, as far as its own limits go.
    /// </summary>
    /// <param name="line">The invoice line.</param>
    public bool IsLimitedTo(CheckoutLineItem line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return LineItemIds is not { Count: > 0 } || LineItemIds.Contains(line.ItemId, StringComparer.Ordinal);
    }
}

/// <summary>
/// The kinds of discount, for the rules about which can be used together.
/// </summary>
public enum DiscountClass
{
    /// <summary>
    /// Off some products or categories.
    /// </summary>
    Product = 0,

    /// <summary>
    /// Off the order as a whole.
    /// </summary>
    Order = 1,

    /// <summary>
    /// Off shipping.
    /// </summary>
    Shipping = 2,
}

/// <summary>
/// The kinds of discount another can be used with.
/// </summary>
[Flags]
public enum DiscountCombination
{
    None = 0,

    Product = 1,

    Order = 2,

    Shipping = 4,

    All = Product | Order | Shipping,
}

public static class DiscountClassExtensions
{
    public static DiscountCombination ToCombination(this DiscountClass discountClass)
        => discountClass switch
        {
            DiscountClass.Product => DiscountCombination.Product,
            DiscountClass.Shipping => DiscountCombination.Shipping,
            _ => DiscountCombination.Order,
        };
}

/// <summary>
/// What part of a checkout a <see cref="DiscountLine"/> reduces.
/// </summary>
public enum DiscountTarget
{
    /// <summary>
    /// The one-time amount: setup fees and one-off goods.
    /// </summary>
    OneTime = 0,

    /// <summary>
    /// The first recurring cycle only. This is the usual shape of an introductory offer, and keeping it
    /// distinct is what stops "first month half price" from silently halving every month after.
    /// </summary>
    FirstCycle = 1,

    /// <summary>
    /// The shipping charged now, such as a free-shipping coupon takes off. No other discount touches shipping.
    /// </summary>
    Shipping = 2,
}
