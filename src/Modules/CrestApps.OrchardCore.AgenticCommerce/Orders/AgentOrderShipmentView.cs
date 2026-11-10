namespace CrestApps.OrchardCore.AgenticCommerce.Orders;

/// <summary>
/// A shipment of an order an agent placed.
/// </summary>
public sealed class AgentOrderShipmentView
{
    public string Id { get; set; }

    public string Status { get; set; }

    public string Carrier { get; set; }

    public string TrackingNumber { get; set; }

    public string TrackingUrl { get; set; }

    public DateTime? ShippedUtc { get; set; }

    public DateTime? DeliveredUtc { get; set; }

    public IDictionary<string, int> Lines { get; set; } = new Dictionary<string, int>();
}
