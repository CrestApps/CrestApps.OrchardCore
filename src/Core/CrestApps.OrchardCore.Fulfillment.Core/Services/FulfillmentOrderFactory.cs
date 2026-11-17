using CrestApps.OrchardCore.Fulfillment.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Products.Core.Models;

namespace CrestApps.OrchardCore.Fulfillment.Core.Services;

/// <summary>
/// Checks that an order may go to fulfillment partners and describes it for them.
/// </summary>
public static class FulfillmentOrderFactory
{
    private static readonly char[] _spaces = [' ', '\t'];

    /// <summary>
    /// Checks an order and describes it for the partners.
    /// </summary>
    /// <param name="order">The order, as stored now.</param>
    /// <returns>The description, or why the order must not be sent.</returns>
    public static FulfillmentOrderCheck Check(Order order)
    {
        if (order is null)
        {
            return FulfillmentOrderCheck.Blocked("The order no longer exists.", nothingToSend: false);
        }

        if (order.Status != OrderStatus.Paid)
        {
            return FulfillmentOrderCheck.Blocked($"The order is {order.Status}, not paid.", nothingToSend: false);
        }

        if (order.RefundStatus == OrderRefundStatus.Refunded)
        {
            return FulfillmentOrderCheck.Blocked("The order was refunded in full.", nothingToSend: false);
        }

        if (order.HasOpenDispute)
        {
            return FulfillmentOrderCheck.Blocked("The order has an open payment dispute.", nothingToSend: false);
        }

        if (order.AnonymizedUtc is not null)
        {
            return FulfillmentOrderCheck.Blocked("The order's personal data was removed.", nothingToSend: false);
        }

        var physical = (order.Lines ?? []).Where(line => line.ProductType == ProductType.Good && line.Quantity > 0).ToList();

        if (physical.Count == 0)
        {
            return FulfillmentOrderCheck.Blocked("Nothing in the order ships.", nothingToSend: true);
        }

        if (order.Shipping?.IsPickup == true)
        {
            return FulfillmentOrderCheck.Blocked("The buyer picks the order up at the store.", nothingToSend: true);
        }

        var address = order.ShippingAddress;

        if (address is null || string.IsNullOrWhiteSpace(address.AddressLine1) || string.IsNullOrWhiteSpace(address.Country))
        {
            return FulfillmentOrderCheck.Blocked("The order has no complete shipping address.", nothingToSend: false);
        }

        if (physical.Any(line => line.BundleItems?.Count > 0))
        {
            return FulfillmentOrderCheck.Blocked("The order contains a bundle, which partners can't make yet. Send it to the partner yourself.", nothingToSend: false);
        }

        var (firstName, lastName) = SplitName(string.IsNullOrWhiteSpace(address.Name) ? order.CustomerName : address.Name);
        var description = new FulfillmentOrder
        {
            OrderId = order.ItemId,
            OrderNumber = order.OrderNumber,
            Recipient = new FulfillmentRecipient
            {
                FirstName = firstName,
                LastName = lastName,
                Email = order.CustomerEmail,
                Phone = string.IsNullOrWhiteSpace(address.Phone) ? order.CustomerPhone : address.Phone,
                Company = address.Company,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                City = address.City,
                Region = address.Region,
                PostalCode = address.PostalCode,
                Country = address.Country,
            },
        };

        foreach (var line in physical)
        {
            description.Lines.Add(new FulfillmentOrderLine
            {
                LineId = line.LineId,
                ContentItemId = line.ContentItemId,
                VariantId = line.VariantId,
                Sku = line.Sku,
                Title = string.IsNullOrWhiteSpace(line.VariantTitle) ? line.Title : $"{line.Title} ({line.VariantTitle})",
                Quantity = line.Quantity,
            });
        }

        return FulfillmentOrderCheck.Ready(description);
    }

    /// <summary>
    /// Splits a full name into a first name and the rest.
    /// </summary>
    /// <param name="name">The full name.</param>
    /// <returns>The first word, and the other words; a single word is used for both.</returns>
    public static (string FirstName, string LastName) SplitName(string name)
    {
        var words = (name ?? string.Empty).Split(_spaces, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words.Length switch
        {
            0 => (null, null),
            1 => (words[0], words[0]),
            _ => (words[0], string.Join(' ', words.Skip(1))),
        };
    }
}
