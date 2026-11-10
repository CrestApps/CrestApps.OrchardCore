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
}
