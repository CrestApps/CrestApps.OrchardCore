using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Orders;

/// <summary>
/// An order an agent placed, in terms every protocol's order updates map to their own.
/// </summary>
public sealed class AgentOrderView
{
    public string Id { get; set; }

    public string Number { get; set; }

    public string CheckoutId { get; set; }

    public string PermalinkUrl { get; set; }

    public string Currency { get; set; }

    public bool Canceled { get; set; }

    public DateTime CreatedUtc { get; set; }

    public Address ShippingAddress { get; set; }

    public string ShippingMethod { get; set; }

    public bool IsPickup { get; set; }

    public IList<AgentOrderLineView> Lines { get; set; } = [];

    public IList<AgentOrderShipmentView> Shipments { get; set; } = [];

    public IList<AgentOrderRefundView> Refunds { get; set; } = [];

    public decimal Subtotal { get; set; }

    public decimal Discount { get; set; }

    public decimal Shipping { get; set; }

    public decimal Tax { get; set; }

    public decimal Total { get; set; }

    public decimal Refunded { get; set; }
}
