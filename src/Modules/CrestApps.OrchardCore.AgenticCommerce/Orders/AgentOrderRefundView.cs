namespace CrestApps.OrchardCore.AgenticCommerce.Orders;

/// <summary>
/// A refund of an order an agent placed.
/// </summary>
public sealed class AgentOrderRefundView
{
    public string Id { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; }

    public DateTime CreatedUtc { get; set; }
}
