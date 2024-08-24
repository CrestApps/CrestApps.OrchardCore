namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class InvoiceLineItem
{
    /// <summary>
    /// A unique identifier for the line item.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Short description of the line item.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// The items quantity.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// The price of each unit.
    /// </summary>
    public double UnitPrice { get; set; }

    /// <summary>
    /// Any recurring payment.
    /// </summary>
    public SubscriptionPlan Subscription { get; set; }

    public double GetLineTotal()
        => Math.Round(Quantity * UnitPrice, 2);
}
