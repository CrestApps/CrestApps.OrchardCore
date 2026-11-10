namespace CrestApps.OrchardCore.AgenticCommerce.Orders;

/// <summary>
/// A line of an order an agent placed.
/// </summary>
public sealed class AgentOrderLineView
{
    public string Id { get; set; }

    public string ItemId { get; set; }

    public string Title { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public int CurrentQuantity { get; set; }

    public int FulfilledQuantity { get; set; }

    public decimal Total { get; set; }
}
