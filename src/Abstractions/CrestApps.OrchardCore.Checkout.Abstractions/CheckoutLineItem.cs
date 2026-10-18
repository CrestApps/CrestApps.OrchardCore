using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// A single priced line on a checkout invoice.
/// </summary>
public sealed class CheckoutLineItem
{
    /// <summary>
    /// A unique identifier for the line item.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// A short description of the line item.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// The quantity being purchased.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// The price of each unit, expressed in major currency units.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// The recurring plan for this line item, or <see langword="null"/> for a one-time charge.
    /// </summary>
    public RecurringPlan Plan { get; set; }

    /// <summary>
    /// Gets or sets the catalog price this line came from, when it came from one at a fixed amount.
    /// </summary>
    public string PriceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the line is left out of the tax determination, because its tax
    /// was already decided elsewhere (see <see cref="BillingItem.ExcludeFromTax"/>).
    /// </summary>
    public bool ExcludeFromTax { get; set; }

    /// <summary>
    /// Gets or sets the stock keeping unit of the product the line is for, when it is for one.
    /// </summary>
    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets the line's own tax category (see <see cref="BillingItem.TaxCategoryCode"/>).
    /// </summary>
    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the line's own tax classification (see <see cref="BillingItem.TaxClassificationCode"/>).
    /// </summary>
    public string TaxClassificationCode { get; set; }

    /// <summary>
    /// Gets or sets what kind of thing the line is for tax purposes (see <see cref="BillingItem.TaxableKind"/>).
    /// </summary>
    public TaxableItemKind? TaxableKind { get; set; }

    /// <summary>
    /// Gets or sets what discounts take off the whole line (all its units), which is left out of what the line is
    /// taxed on. Set when the invoice is built.
    /// </summary>
    public decimal DiscountAmount { get; set; }

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

    /// <summary>
    /// Returns the line total rounded to the precision of the supplied currency. Rounding at the currency's
    /// own scale keeps zero-decimal (for example JPY) and three-decimal (for example KWD) currencies exact.
    /// </summary>
    /// <param name="currency">The ISO-4217 currency code used to determine rounding precision.</param>
    public decimal GetLineTotal(string currency = null)
        => Money.Round(Quantity * UnitPrice, currency);
}
