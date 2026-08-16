namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents one chargeable item on a subscription invoice.
/// </summary>
public class InvoiceLineItem
{
    /// <summary>
    /// Gets or sets the unique identifier for the line item.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the short description of the line item.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the item quantity.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the price of each unit.
    /// </summary>
    public double UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the subscription billing plan when the line item recurs.
    /// </summary>
    public SubscriptionPlan Subscription { get; set; }

    /// <summary>
    /// Calculates the total amount for the line item.
    /// </summary>
    /// <returns>The rounded line total based on quantity and unit price.</returns>
    public double GetLineTotal()
        => Math.Round(Quantity * UnitPrice, 2);
}
