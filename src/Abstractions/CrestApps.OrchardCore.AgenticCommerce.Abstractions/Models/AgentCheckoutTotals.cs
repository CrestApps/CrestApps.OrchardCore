namespace CrestApps.OrchardCore.AgenticCommerce.Models;

/// <summary>
/// The totals of an agent checkout, in major units of its currency.
/// </summary>
public sealed class AgentCheckoutTotals
{
    /// <summary>
    /// Gets or sets the items' total, before discounts.
    /// </summary>
    public decimal Subtotal { get; set; }

    /// <summary>
    /// Gets or sets the discounts, as a positive amount.
    /// </summary>
    public decimal Discount { get; set; }

    /// <summary>
    /// Gets or sets the shipping.
    /// </summary>
    public decimal Fulfillment { get; set; }

    /// <summary>
    /// Gets or sets the tax added on top.
    /// </summary>
    public decimal Tax { get; set; }

    /// <summary>
    /// Gets or sets other fees.
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    /// Gets or sets what the buyer pays.
    /// </summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Builds totals whose parts add up to <paramref name="total"/>: an amount the parts don't explain is a fee, and
    /// when the parts come to more than the total, the tax was already in the prices and is lowered by the difference.
    /// </summary>
    /// <param name="subtotal">The items' total, before discounts.</param>
    /// <param name="discount">The discounts, as a positive amount.</param>
    /// <param name="fulfillment">The shipping.</param>
    /// <param name="tax">The tax.</param>
    /// <param name="total">What the buyer pays.</param>
    public static AgentCheckoutTotals Balance(decimal subtotal, decimal discount, decimal fulfillment, decimal tax, decimal total)
    {
        var difference = total - (subtotal - discount + fulfillment + tax);
        var fee = 0m;

        if (difference > 0)
        {
            fee = difference;
        }
        else if (difference < 0)
        {
            tax = Math.Max(0, tax + difference);
        }

        return new AgentCheckoutTotals
        {
            Subtotal = subtotal,
            Discount = discount,
            Fulfillment = fulfillment,
            Tax = tax,
            Fee = fee,
            Total = total,
        };
    }
}
