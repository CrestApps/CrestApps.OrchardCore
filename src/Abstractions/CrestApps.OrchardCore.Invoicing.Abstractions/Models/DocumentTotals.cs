namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// The totals of a document, computed by the document calculator. <see cref="Total"/> is
/// <see cref="Subtotal"/> − <see cref="DiscountTotal"/> + <see cref="ChargeTotal"/> + <see cref="TaxTotal"/> +
/// <see cref="RoundingAmount"/>.
/// </summary>
public sealed class DocumentTotals
{
    /// <summary>
    /// Gets or sets the sum of the line nets, after the line discounts, without tax.
    /// </summary>
    public decimal Subtotal { get; set; }

    /// <summary>
    /// Gets or sets the discounts on the whole document, net of tax, as a positive amount.
    /// </summary>
    public decimal DiscountTotal { get; set; }

    /// <summary>
    /// Gets or sets the shipping and surcharges, net of tax.
    /// </summary>
    public decimal ChargeTotal { get; set; }

    /// <summary>
    /// Gets or sets the tax, the sum of the breakdown.
    /// </summary>
    public decimal TaxTotal { get; set; }

    /// <summary>
    /// Gets or sets the cash rounding added to reach an amount the currency can be paid in, positive or negative.
    /// </summary>
    public decimal RoundingAmount { get; set; }

    /// <summary>
    /// Gets or sets the amount the document asks for.
    /// </summary>
    public decimal Total { get; set; }
}
