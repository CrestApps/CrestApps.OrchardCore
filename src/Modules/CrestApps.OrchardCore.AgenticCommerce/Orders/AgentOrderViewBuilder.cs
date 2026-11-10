using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;

namespace CrestApps.OrchardCore.AgenticCommerce.Orders;

/// <summary>
/// Builds the view of an order an agent placed, with its shipments when the Shipping module is on.
/// </summary>
public sealed class AgentOrderViewBuilder
{
    private readonly IAgentItemResolver _items;
    private readonly IEnumerable<IShipmentStore> _shipments;
    private readonly IEnumerable<IOrderLinkService> _orderLinks;
    private readonly AgentSiteUrls _urls;

    public AgentOrderViewBuilder(
        IAgentItemResolver items,
        IEnumerable<IShipmentStore> shipments,
        IEnumerable<IOrderLinkService> orderLinks,
        AgentSiteUrls urls)
    {
        _items = items;
        _shipments = shipments;
        _orderLinks = orderLinks;
        _urls = urls;
    }

    /// <summary>
    /// Returns the view of an order.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="checkout">The agent checkout that placed it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<AgentOrderView> BuildAsync(Order order, AgentCheckout checkout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        var shipmentStore = _shipments.FirstOrDefault();
        var shipments = shipmentStore is null ? [] : await shipmentStore.ListByOrderAsync(order.ItemId, cancellationToken);
        var links = _orderLinks.FirstOrDefault();
        var fulfilled = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var shipment in shipments.Where(entry => entry.Status is ShipmentStatus.Shipped or ShipmentStatus.Delivered))
        {
            foreach (var line in shipment.Lines)
            {
                fulfilled[line.LineId] = fulfilled.GetValueOrDefault(line.LineId) + line.Quantity;
            }
        }

        var canceled = order.Status == OrderStatus.Canceled;
        var view = new AgentOrderView
        {
            Id = order.ItemId,
            Number = order.OrderNumber,
            CheckoutId = checkout?.ItemId,
            PermalinkUrl = links is null ? await _urls.GetBaseUrlAsync() : await links.CreateUrlAsync(order),
            Currency = order.Currency,
            Canceled = canceled,
            CreatedUtc = order.PaidUtc ?? order.CreatedUtc,
            ShippingAddress = order.ShippingAddress,
            ShippingMethod = order.Shipping?.Name,
            IsPickup = order.Shipping?.IsPickup == true,
            Subtotal = order.Subtotal,
            Discount = order.Discounts?.Sum(discount => discount.Amount) ?? 0,
            Shipping = order.Shipping?.Amount ?? 0,
            Tax = order.TaxAmount ?? 0,
            Total = order.Total ?? order.Subtotal,
            Refunded = order.Refunds?.Sum(refund => refund.Amount) ?? 0,
        };

        foreach (var line in order.Lines)
        {
            view.Lines.Add(new AgentOrderLineView
            {
                Id = line.LineId,
                ItemId = _items.GetItemId(line.ContentItemId, line.VariantId, line.Sku),
                Title = string.IsNullOrEmpty(line.VariantTitle) ? line.Title : $"{line.Title} ({line.VariantTitle})",
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity,
                CurrentQuantity = canceled ? 0 : line.Quantity,
                FulfilledQuantity = Math.Min(line.Quantity, fulfilled.GetValueOrDefault(line.LineId)),
                Total = line.LineTotal,
            });
        }

        foreach (var shipment in shipments)
        {
            view.Shipments.Add(new AgentOrderShipmentView
            {
                Id = shipment.ItemId,
                Status = shipment.Status.ToString(),
                Carrier = shipment.Carrier,
                TrackingNumber = shipment.TrackingNumber,
                TrackingUrl = shipment.TrackingUrl,
                ShippedUtc = shipment.ShippedUtc,
                DeliveredUtc = shipment.DeliveredUtc,
                Lines = shipment.Lines.GroupBy(line => line.LineId).ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity)),
            });
        }

        foreach (var refund in order.Refunds ?? [])
        {
            view.Refunds.Add(new AgentOrderRefundView
            {
                Id = refund.RefundId,
                Amount = refund.Amount,
                Reason = refund.Reason,
                CreatedUtc = refund.CreatedUtc,
            });
        }

        return view;
    }
}
