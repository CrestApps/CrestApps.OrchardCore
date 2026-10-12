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
}
