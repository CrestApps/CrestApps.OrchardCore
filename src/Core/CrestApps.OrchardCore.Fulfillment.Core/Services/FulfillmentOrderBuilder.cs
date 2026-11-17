using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Shipping.Services;

namespace CrestApps.OrchardCore.Fulfillment.Core.Services;

/// <summary>
/// Checks that an order may go to fulfillment partners and describes what is left to ship: the units Shipping has
/// not put in a shipment yet (returned shipments count as not shipped).
/// </summary>
public sealed class FulfillmentOrderBuilder
{
    private readonly IShipmentService _shipments;

    /// <summary>
    /// Initializes a new instance of the <see cref="FulfillmentOrderBuilder"/> class.
    /// </summary>
    /// <param name="shipments">The shipment service, for the quantities left to ship.</param>
    public FulfillmentOrderBuilder(IShipmentService shipments)
    {
        _shipments = shipments;
    }

    /// <summary>
    /// Checks an order and describes what is left to ship.
    /// </summary>
    /// <param name="order">The order, as stored now.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The description, or why the order must not be sent.</returns>
    public async Task<FulfillmentOrderCheck> CheckAsync(Order order, CancellationToken cancellationToken = default)
    {
        var blocked = CheckOrder(order);

        if (blocked is not null)
        {
            return blocked;
        }

        var remaining = await _shipments.GetRemainingAsync(order, cancellationToken);

        return Describe(order, remaining);
    }

    /// <summary>
    /// Checks the order itself, before what is left to ship is known.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <returns>Why the order must not be sent, or <see langword="null"/> when it may.</returns>
    public static FulfillmentOrderCheck CheckOrder(Order order)
    {
        if (order is null)
        {
            return FulfillmentOrderCheck.Blocked(null, FulfillmentReasons.NotPaid, nothingToSend: false, "Deleted");
        }

        if (order.Status != OrderStatus.Paid)
        {
            return FulfillmentOrderCheck.Blocked(order, FulfillmentReasons.NotPaid, nothingToSend: false, order.Status.ToString());
        }

        if (order.RefundStatus == OrderRefundStatus.Refunded)
        {
            return FulfillmentOrderCheck.Blocked(order, FulfillmentReasons.Refunded, nothingToSend: false);
        }

        if (order.HasOpenDispute)
        {
            return FulfillmentOrderCheck.Blocked(order, FulfillmentReasons.OpenDispute, nothingToSend: false);
        }

        if (order.AnonymizedUtc is not null)
        {
            return FulfillmentOrderCheck.Blocked(order, FulfillmentReasons.Anonymized, nothingToSend: false);
        }

        if (order.Shipping?.IsPickup == true)
        {
            return FulfillmentOrderCheck.Blocked(order, FulfillmentReasons.Pickup, nothingToSend: true);
        }

        var address = order.ShippingAddress;

        if (address is null || string.IsNullOrWhiteSpace(address.AddressLine1) || string.IsNullOrWhiteSpace(address.Country))
        {
            return FulfillmentOrderCheck.Blocked(order, FulfillmentReasons.NoAddress, nothingToSend: false);
        }

        return null;
    }

    /// <summary>
    /// Describes an order with the quantities left to ship.
    /// </summary>
    /// <param name="order">The order, already checked by <see cref="CheckOrder"/>.</param>
    /// <param name="remaining">The quantity left to ship for each physical line.</param>
    /// <returns>The description, or why there is nothing to send.</returns>
    public static FulfillmentOrderCheck Describe(Order order, IReadOnlyDictionary<string, int> remaining)
    {
        ArgumentNullException.ThrowIfNull(order);

        var lines = (order.Lines ?? [])
            .Where(line => ShippingOrderRateService.IsPhysical(line) && remaining?.GetValueOrDefault(line.LineId) > 0)
            .ToList();

        if (lines.Count == 0)
        {
            return FulfillmentOrderCheck.Blocked(order, FulfillmentReasons.NothingLeft, nothingToSend: true);
        }

        if (lines.Any(line => line.BundleItems?.Count > 0))
        {
            return FulfillmentOrderCheck.Blocked(order, FulfillmentReasons.Bundle, nothingToSend: false);
        }

        var quantities = lines
            .GroupBy(line => line.LineId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => remaining[group.Key], StringComparer.Ordinal);

        return FulfillmentOrderCheck.Ready(order, ForLines(order, quantities));
    }

    /// <summary>
    /// Describes an order holding only the given lines and quantities, such as the lines assigned to one partner or the
    /// lines of a shipment already reserved for it.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="lines">The quantity of each line identifier.</param>
    /// <returns>The description.</returns>
    public static FulfillmentOrder ForLines(Order order, IReadOnlyDictionary<string, int> lines)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(lines);

        var description = new FulfillmentOrder
        {
            OrderId = order.ItemId,
            OrderNumber = order.OrderNumber,
            ShippingAddress = order.ShippingAddress,
            Email = order.CustomerEmail,
            Phone = string.IsNullOrWhiteSpace(order.ShippingAddress?.Phone)
                ? order.CustomerPhone
                : order.ShippingAddress.Phone,
        };

        foreach (var line in (order.Lines ?? []).GroupBy(line => line.LineId, StringComparer.Ordinal).Select(group => group.First()))
        {
            if (lines.TryGetValue(line.LineId, out var quantity) && quantity > 0)
            {
                description.Lines.Add(new FulfillmentOrderLine
                {
                    LineId = line.LineId,
                    ContentItemId = line.ContentItemId,
                    VariantId = line.VariantId,
                    Sku = line.Sku,
                    Title = string.IsNullOrWhiteSpace(line.VariantTitle) ? line.Title : $"{line.Title} ({line.VariantTitle})",
                    Quantity = quantity,
                });
            }
        }

        return description;
    }
}
