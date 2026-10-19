namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// Part of a checkout's total paid from a balance the buyer holds, such as a gift card or store credit, rather than
/// charged by the payment provider. It is not a discount: tax is worked out on the full price first.
/// </summary>
public sealed class TenderLine
{
    /// <summary>
    /// Gets or sets the key of the <see cref="Services.ICheckoutTenderProvider"/> the balance belongs to.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets which of the provider's balances pays, such as a gift card's identifier. It is never a secret the
    /// buyer typed, such as the gift card's full code.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets what the buyer sees, such as "Gift card ending 4F2K".
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how much of the total the balance pays, tax included.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the part of <see cref="Amount"/> that is tax. The payment provider carries the tax first, so this
    /// is only more than zero when a balance pays for more than the items' share.
    /// </summary>
    public decimal TaxAmount { get; set; }
}
