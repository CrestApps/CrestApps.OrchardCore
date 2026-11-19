using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Returns one order as its page on Commerce › Orders shows it to staff, from an explicit list of fields: the customer
/// and addresses, lines, discounts, shipping, tax, totals, how it was paid, notes, refunds, disputes, payment risk, what
/// can still be refunded and what is still owed (<see cref="IOrderOperationsService.GetMoneyAsync"/>), and links to its
/// page, invoice and packing slip. Narrower than the catalog row: the pay link is a key anyone holding it can pay or
/// see the order with, so only <c>hasPayLink</c> is returned and the order's page shows the link; the customer's and
/// the addresses' phone numbers, the tenders' references (a gift card's can be spent) and the checkout's raw details are
/// never returned.
/// </summary>
internal sealed class GetOrderTool : OrdersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getOrder";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "orderId": {
          "type": "string",
          "description": "The order's id, from listOrders."
        },
        "orderNumber": {
          "type": "string",
          "description": "The number the buyer was given, such as 1042, when the id is not known."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one order by id or order number: customer and addresses, lines, discounts, tax, totals, how it was paid, notes, refunds, disputes, payment risk, what can still be refunded and what is still owed, plus links to its admin page, invoice and packing slip, and whether it has a pay link. Use it before refunding, canceling, shipping or answering a customer.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var (order, error) = await FindOrderAsync(services, arguments, cancellationToken);

        if (order is null)
        {
            return error;
        }

        var operations = services.GetRequiredService<IOrderOperationsService>();
        var money = await operations.GetMoneyAsync(order, cancellationToken);
        var notes = new List<object>();

        foreach (var note in ReadNotes(order))
        {
            notes.Add(new
            {
                text = note.Text,
                kind = ToCamel(note.Kind.ToString()),
                by = note.AuthorName,
                when = await LocalTimeAsync(services, note.CreatedUtc),
            });
        }

        var refunds = new List<object>();

        foreach (var refund in order.Refunds ?? [])
        {
            refunds.Add(new
            {
                amount = refund.Amount,
                currency = refund.Currency,
                reason = refund.Reason,
                when = await LocalTimeAsync(services, refund.CreatedUtc),
            });
        }

        var disputes = new List<object>();

        foreach (var dispute in order.Disputes ?? [])
        {
            disputes.Add(new
            {
                amount = dispute.Amount,
                currency = dispute.Currency,
                reason = dispute.Reason,
                status = dispute.Status,
                closed = dispute.IsClosed,
                gateway = dispute.Gateway,
                when = await LocalTimeAsync(services, dispute.CreatedUtc),
            });
        }

        var isDraft = order.Status == OrderStatus.Draft;

        return Json(new
        {
            orderId = order.ItemId,
            number = order.OrderNumber,
            status = ToCamel(order.Status.ToString()),
            channel = order.Channel,
            placed = await LocalTimeAsync(services, order.CreatedUtc),
            paid = await LocalTimeAsync(services, order.PaidUtc),
            canceled = order.Status == OrderStatus.Canceled
                ? new
                {
                    when = await LocalTimeAsync(services, order.CanceledUtc),
                    reason = order.CancelReason is { } reason ? ToCamel(reason.ToString()) : null,
                    note = order.CancelNote,
                }
                : null,
            customer = new
            {
                customerId = order.CustomerId,
                name = order.CustomerName,
                email = order.CustomerEmail,
                hasAccount = order.UserId is not null,
                anonymized = order.AnonymizedUtc is not null ? true : (bool?)null,
            },
            billingAddress = DescribeAddress(order.BillingAddress),
            shippingAddress = DescribeAddress(order.ShippingAddress),
            purchaseOrderNumber = order.PurchaseOrderNumber,
            currency = order.Currency,
            lines = order.Lines.Select(line => new
            {
                lineId = line.LineId,
                productId = line.ContentItemId,
                title = line.Title,
                variant = line.VariantTitle,
                variantId = line.VariantId,
                sku = line.Sku,
                quantity = line.Quantity,
                unitPrice = line.UnitPrice,
                quotedUnitPrice = line.QuotedUnitPrice,
                lineTotal = line.LineTotal,
            }).ToArray(),
            subtotal = order.Subtotal,
            discounts = (order.Discounts ?? []).Select(discount => new
            {
                code = discount.Code,
                description = discount.Description,
                amount = discount.Amount,
            }).ToArray(),
            shipping = order.Shipping is { } shipping
                ? new
                {
                    method = shipping.Name,
                    amount = shipping.Amount,
                    pickup = shipping.IsPickup,
                }
                : null,
            tax = order.TaxAmount,
            taxLines = (order.TaxLines ?? []).Select(tax => new
            {
                name = tax.TaxName,
                jurisdiction = tax.JurisdictionName,
                rate = tax.Rate,
                amount = tax.TaxAmount,
            }).ToArray(),
            total = order.Total,
            paidWith = (order.Tenders ?? []).Select(tender => new
            {
                description = tender.Description,
                amount = tender.Amount,
            }).ToArray(),
            throughCheckout = !string.IsNullOrEmpty(order.CheckoutSessionId),
            fulfillment = order.FulfillmentStatus is { } fulfillment ? ToCamel(fulfillment.ToString()) : null,
            refundable = money.Refundable,
            owed = money.Outstanding,
            canRefundAsStoreCredit = money.CanRefundAsStoreCredit,
            refunded = order.RefundedAmount,
            refunds,
            disputes,
            paymentRisk = order.PaymentRisk is { } risk
                ? new
                {
                    level = risk.Level,
                    score = risk.Score,
                    elevated = risk.IsElevated,
                }
                : null,
            invoice = order.Invoice is { } invoice
                ? new
                {
                    note = invoice.Note,
                    payBy = invoice.DueUtc?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    sent = await LocalTimeAsync(services, invoice.SentUtc),
                    addsToOrderNumber = invoice.AddsToOrderNumber,
                }
                : null,
            quote = order.Quote is { } quote
                ? new
                {
                    status = ToCamel(quote.Status.ToString()),
                    message = quote.Message,
                    requested = await LocalTimeAsync(services, quote.RequestedUtc),
                    expires = await LocalTimeAsync(services, quote.ExpiresUtc),
                }
                : null,
            hasPayLink = services.GetService<OrderPayLinks>()?.CanPayByLink(order) == true,
            notes,
            links = new
            {
                adminPage = GetAdminPath(services, "OrdersDetail", new { orderId = order.ItemId }),
                invoice = isDraft ? null : GetAdminPath(services, "OrdersInvoice", new { orderId = order.ItemId }),
                packingSlip = isDraft ? null : GetAdminPath(services, "OrdersPackingSlip", new { orderId = order.ItemId }),
            },
        });
    }

    // The order's notes as this branch keeps them, on the order. Once notes move to the order's timeline, only this
    // method changes.
    private static IEnumerable<OrderNote> ReadNotes(Order order)
        => order.Notes ?? [];

    private static object DescribeAddress(Address address)
        => address is null
            ? null
            : new
            {
                name = address.Name,
                company = address.Company,
                line1 = address.AddressLine1,
                line2 = address.AddressLine2,
                city = address.City,
                region = address.Region,
                postalCode = address.PostalCode,
                country = address.Country,
            };
}
