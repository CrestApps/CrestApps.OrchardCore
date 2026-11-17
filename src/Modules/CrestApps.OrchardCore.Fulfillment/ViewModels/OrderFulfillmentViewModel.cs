namespace CrestApps.OrchardCore.Fulfillment.ViewModels;

/// <summary>
/// The fulfillment partners card on an order's admin page.
/// </summary>
public class OrderFulfillmentViewModel
{
    /// <summary>
    /// Gets or sets the order identifier.
    /// </summary>
    public string OrderId { get; set; }

    /// <summary>
    /// Gets or sets the order number.
    /// </summary>
    public string OrderNumber { get; set; }

    /// <summary>
    /// Gets or sets what was sent to each partner.
    /// </summary>
    public IList<OrderFulfillmentEntry> Requests { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of connected partners the order was not sent to yet.
    /// </summary>
    public IList<string> NotSentYet { get; set; } = [];
}
