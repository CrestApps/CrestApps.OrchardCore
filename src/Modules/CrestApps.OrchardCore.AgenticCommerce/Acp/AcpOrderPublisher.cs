using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Orders;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// Sends ACP order updates (<c>order_create</c>, <c>order_update</c>) with the full order to the platform's order updates
/// address, signed with its secret (<c>Merchant-Signature</c>).
/// </summary>
public sealed class AcpOrderPublisher : IAgentOrderPublisher
{
    private readonly IAgentPlatformStore _platforms;
    private readonly AgentOrderViewBuilder _views;
    private readonly IWebhookOutbox _outbox;

    public AcpOrderPublisher(IAgentPlatformStore platforms, AgentOrderViewBuilder views, IWebhookOutbox outbox)
    {
        _platforms = platforms;
        _views = views;
        _outbox = outbox;
    }

    /// <inheritdoc />
    public string Protocol
        => AgenticCommerceConstants.Protocols.Acp;

    /// <inheritdoc />
    public async Task PublishAsync(AgentCheckout checkout, Order order, bool created, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkout);

        var platform = await _platforms.FindByIdAsync(checkout.PlatformId, cancellationToken);

        if (platform is null || string.IsNullOrEmpty(platform.WebhookUrl) || string.IsNullOrEmpty(platform.ProtectedWebhookSecret))
        {
            return;
        }

        var view = await _views.BuildAsync(order, checkout, cancellationToken);
        var eventName = created
            ? platform.AcpOrderCreatedEvent ?? "order_create"
            : platform.AcpOrderUpdatedEvent ?? "order_update";

        await _outbox.EnqueueAsync(new WebhookDirectDelivery
        {
            OwnerId = platform.ItemId,
            Url = platform.WebhookUrl,
            EventName = eventName,
            Body = new JsonObject
            {
                ["type"] = eventName,
                ["data"] = Write(view),
            }.ToJsonString(JsonSerializerOptions.Web),
            FormatterName = AcpOrderWebhookFormatter.FormatterName,
            Data = new Dictionary<string, string>
            {
                [AcpConstants.PlatformIdData] = platform.ItemId,
            },
        }, cancellationToken);
    }

    /// <summary>
    /// Returns an order as ACP's <c>Order</c>.
    /// </summary>
    /// <param name="order">The order.</param>
    public static JsonObject Write(AgentOrderView order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var currency = order.Currency;
        var lines = new JsonArray();

        foreach (var line in order.Lines)
        {
            lines.Add(new JsonObject
            {
                ["id"] = line.Id,
                ["title"] = line.Title,
                ["product_id"] = line.ItemId,
                ["unit_price"] = AgentJson.ToMinor(line.UnitPrice, currency),
                ["subtotal"] = AgentJson.ToMinor(line.Total, currency),
                ["quantity"] = new JsonObject
                {
                    ["ordered"] = line.Quantity,
                    ["current"] = line.CurrentQuantity,
                    ["fulfilled"] = line.FulfilledQuantity,
                },
                ["status"] = line.CurrentQuantity == 0
                    ? "removed"
                    : line.FulfilledQuantity >= line.CurrentQuantity ? "fulfilled" : line.FulfilledQuantity > 0 ? "partial" : "processing",
            });
        }

        var fulfillments = new JsonArray();

        foreach (var shipment in order.Shipments)
        {
            var events = new JsonArray();

            if (shipment.ShippedUtc is { } shipped)
            {
                events.Add(new JsonObject { ["id"] = shipment.Id + "_shipped", ["type"] = "shipped", ["occurred_at"] = AgentJson.ToRfc3339(shipped) });
            }

            if (shipment.DeliveredUtc is { } delivered)
            {
                events.Add(new JsonObject { ["id"] = shipment.Id + "_delivered", ["type"] = "delivered", ["occurred_at"] = AgentJson.ToRfc3339(delivered) });
            }

            var fulfillment = new JsonObject
            {
                ["id"] = shipment.Id,
                ["type"] = order.IsPickup ? "pickup" : "shipping",
                ["status"] = shipment.DeliveredUtc is not null ? "delivered" : shipment.ShippedUtc is not null ? "shipped" : "processing",
                ["line_items"] = new JsonArray([.. shipment.Lines.Select(line => (JsonNode)new JsonObject { ["id"] = line.Key, ["quantity"] = line.Value })]),
                ["events"] = events,
            };

            AgentJson.AddIf(fulfillment, "carrier", shipment.Carrier);
            AgentJson.AddIf(fulfillment, "tracking_number", shipment.TrackingNumber);
            AgentJson.AddIf(fulfillment, "tracking_url", shipment.TrackingUrl);
            fulfillments.Add(fulfillment);
        }

        var adjustments = new JsonArray();

        foreach (var refund in order.Refunds)
        {
            var adjustment = new JsonObject
            {
                ["id"] = refund.Id,
                ["type"] = "refund",
                ["occurred_at"] = AgentJson.ToRfc3339(refund.CreatedUtc),
                ["status"] = "completed",
                ["amount"] = AgentJson.ToMinor(refund.Amount, currency),
                ["currency"] = currency?.ToLowerInvariant(),
            };

            AgentJson.AddIf(adjustment, "reason", refund.Reason);
            adjustments.Add(adjustment);
        }

        var allFulfilled = order.Lines.Count > 0 && order.Lines.All(line => line.CurrentQuantity == 0 || line.FulfilledQuantity >= line.CurrentQuantity);
        var allDelivered = order.Shipments.Count > 0 && order.Shipments.All(shipment => shipment.DeliveredUtc is not null) && allFulfilled;
        var totals = new JsonArray
        {
            AcpCheckoutMapper.Total("subtotal", "Subtotal", order.Subtotal - order.Discount, currency),
        };

        if (order.Shipping != 0)
        {
            totals.Add(AcpCheckoutMapper.Total("fulfillment", "Shipping", order.Shipping, currency));
        }

        totals.Add(AcpCheckoutMapper.Total("total", "Total", order.Total, currency));

        if (order.Refunded != 0)
        {
            totals.Add(AcpCheckoutMapper.Total("amount_refunded", "Refunded", order.Refunded, currency));
        }

        return new JsonObject
        {
            ["type"] = "order",
            ["id"] = order.Id,
            ["checkout_session_id"] = order.CheckoutId,
            ["permalink_url"] = order.PermalinkUrl,
            ["order_number"] = order.Number,
            ["status"] = order.Canceled ? "canceled" : allDelivered ? "delivered" : allFulfilled ? "shipped" : order.Shipments.Count > 0 ? "processing" : "confirmed",
            ["line_items"] = lines,
            ["fulfillments"] = fulfillments,
            ["adjustments"] = adjustments,
            ["totals"] = totals,
        };
    }
}
