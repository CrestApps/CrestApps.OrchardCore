using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Orders;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Writes an order an agent placed as a UCP order: its full current state, never a change.
/// </summary>
public static class UcpOrderMapper
{
    /// <summary>
    /// Returns the order as UCP JSON.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="version">The UCP version.</param>
    public static JsonObject Write(AgentOrderView order, string version)
    {
        ArgumentNullException.ThrowIfNull(order);

        var currency = order.Currency;
        var lines = new JsonArray();

        foreach (var line in order.Lines)
        {
            var status = line.CurrentQuantity == 0
                ? "removed"
                : line.FulfilledQuantity >= line.CurrentQuantity
                    ? "fulfilled"
                    : line.FulfilledQuantity > 0 ? "partial" : "processing";

            lines.Add(new JsonObject
            {
                ["id"] = line.Id,
                ["item"] = new JsonObject
                {
                    ["id"] = line.ItemId,
                    ["title"] = line.Title,
                    ["price"] = AgentJson.ToMinor(line.UnitPrice, currency),
                },
                ["quantity"] = new JsonObject
                {
                    ["original"] = line.Quantity,
                    ["total"] = line.CurrentQuantity,
                    ["fulfilled"] = line.FulfilledQuantity,
                },
                ["totals"] = new JsonArray(new JsonObject { ["type"] = "total", ["amount"] = AgentJson.ToMinor(line.Total, currency) }),
                ["status"] = status,
            });
        }

        var expectation = new JsonObject
        {
            ["id"] = "exp_1",
            ["line_items"] = new JsonArray([.. order.Lines.Select(line => (JsonNode)new JsonObject { ["id"] = line.Id, ["quantity"] = line.CurrentQuantity })]),
            ["method_type"] = order.IsPickup ? "pickup" : "shipping",
            ["fulfillable_on"] = "now",
        };

        AgentJson.AddIf(expectation, "description", order.ShippingMethod);

        if (order.ShippingAddress is not null)
        {
            expectation["destination"] = UcpCheckoutMapper.WriteAddress(order.ShippingAddress);
        }

        var events = new JsonArray();

        foreach (var shipment in order.Shipments)
        {
            foreach (var (type, utc) in new[] { ("shipped", shipment.ShippedUtc), ("delivered", shipment.DeliveredUtc) })
            {
                if (utc is null)
                {
                    continue;
                }

                var entry = new JsonObject
                {
                    ["id"] = $"{shipment.Id}_{type}",
                    ["occurred_at"] = AgentJson.ToRfc3339(utc.Value),
                    ["type"] = type,
                    ["line_items"] = new JsonArray([.. shipment.Lines.Select(line => (JsonNode)new JsonObject { ["id"] = line.Key, ["quantity"] = line.Value })]),
                };

                AgentJson.AddIf(entry, "tracking_number", shipment.TrackingNumber);
                AgentJson.AddIf(entry, "tracking_url", shipment.TrackingUrl);
                AgentJson.AddIf(entry, "carrier", shipment.Carrier);
                events.Add(entry);
            }
        }

        if (order.Canceled)
        {
            events.Add(new JsonObject
            {
                ["id"] = "canceled",
                ["occurred_at"] = AgentJson.ToRfc3339(order.CreatedUtc),
                ["type"] = "canceled",
            });
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
                ["totals"] = new JsonArray(new JsonObject { ["type"] = "total", ["amount"] = -AgentJson.ToMinor(refund.Amount, currency) }),
            };

            AgentJson.AddIf(adjustment, "description", refund.Reason);
            adjustments.Add(adjustment);
        }

        var json = new JsonObject
        {
            ["ucp"] = new JsonObject
            {
                ["version"] = version,
                ["capabilities"] = new JsonObject
                {
                    [UcpConstants.Capabilities.Order] = new JsonArray(new JsonObject { ["version"] = version }),
                },
            },
            ["id"] = order.Id,
            ["checkout_id"] = order.CheckoutId,
            ["label"] = order.Number,
            ["permalink_url"] = order.PermalinkUrl,
            ["currency"] = currency?.ToUpperInvariant(),
            ["line_items"] = lines,
            ["fulfillment"] = new JsonObject
            {
                ["expectations"] = new JsonArray(expectation),
                ["events"] = events,
            },
            ["totals"] = UcpCheckoutMapper.WriteTotals(new CrestApps.OrchardCore.AgenticCommerce.Models.AgentCheckoutTotals
            {
                Subtotal = order.Subtotal,
                Discount = order.Discount,
                Fulfillment = order.Shipping,
                Tax = Math.Max(0, order.Total - (order.Subtotal - order.Discount + order.Shipping)),
                Total = order.Total,
            }, currency),
        };

        if (adjustments.Count > 0)
        {
            json["adjustments"] = adjustments;
        }

        return json;
    }
}
