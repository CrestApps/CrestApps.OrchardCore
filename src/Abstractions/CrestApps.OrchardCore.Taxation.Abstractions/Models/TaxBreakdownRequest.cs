namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// What a document's tax breakdown is worked out from: its items (lines and charges) after their discounts, each with
/// the taxes that apply to it, and how the document's prices are given.
/// </summary>
public sealed class TaxBreakdownRequest
{
    /// <summary>
    /// Gets or sets the ISO-4217 currency the amounts are in, for rounding.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets whether the item amounts include tax, so the tax is taken out of them rather than added on top.
    /// </summary>
    public bool PricesIncludeTax { get; set; }

    /// <summary>
    /// Gets or sets how tax applies to the document's items that set no treatment of their own, such as the bill-to's
    /// (a party that is exempt or reverse charged).
    /// </summary>
    public TaxTreatment Treatment { get; set; }

    /// <summary>
    /// Gets or sets why the document's items are not taxed, when <see cref="Treatment"/> is not taxable.
    /// </summary>
    public string ExemptionReason { get; set; }

    /// <summary>
    /// Gets or sets the level tax is rounded at: the site's (<see cref="TaxCalculationSettings"/>), the same the checkout
    /// rounds at.
    /// </summary>
    public TaxRoundingLevel RoundingLevel { get; set; } = TaxRoundingLevel.Rate;

    /// <summary>
    /// Gets or sets the step the total is rounded to for payment in cash, such as 0.05, or <see langword="null"/>.
    /// </summary>
    public decimal? CashRoundingIncrement { get; set; }

    /// <summary>
    /// Gets the items: the document's lines and charges.
    /// </summary>
    public IList<TaxBreakdownItem> Items { get; } = [];
}
