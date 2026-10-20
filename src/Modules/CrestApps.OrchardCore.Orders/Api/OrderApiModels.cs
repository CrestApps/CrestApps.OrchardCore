using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Api;

/// <summary>
/// An order in a list.
/// </summary>
/// <param name="Status"><c>draft</c>, <c>awaitingPayment</c>, <c>paid</c> or <c>canceled</c>.</param>
/// <param name="Fulfillment"><c>unfulfilled</c>, <c>partiallyFulfilled</c> or <c>fulfilled</c>, once it's paid.</param>
/// <param name="IsOwed">Whether it's waiting for money: placed to be paid offline, or sent to be paid.</param>
public sealed record ApiOrderSummary(
    string Id,
    string Number,
    string Status,
    string Fulfillment,
    bool IsOwed,
    int ItemCount,
    ApiMoney Total,
    string CustomerName,
    string CustomerEmail,
    DateTime CreatedUtc,
    DateTime? PaidUtc);

/// <summary>
/// An order: what was bought, where it goes, and what was paid and refunded.
/// </summary>
/// <param name="Refund"><c>none</c>, <c>partiallyRefunded</c> or <c>refunded</c>.</param>
/// <param name="PayUrl">A page the buyer pays an order that's still owed on.</param>
public sealed record ApiOrder(
    string Id,
    string Number,
    string Status,
    string Fulfillment,
    string Refund,
    bool IsOwed,
    string Currency,
    string CustomerId,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    ApiAddress BillingAddress,
    ApiAddress ShippingAddress,
    ApiOrderShipping Shipping,
    IReadOnlyList<ApiOrderLine> Lines,
    IReadOnlyList<ApiOrderDiscount> Discounts,
    IReadOnlyList<ApiOrderTender> Tenders,
    IReadOnlyList<ApiOrderRefund> Refunds,
    ApiOrderTotals Totals,
    string PayUrl,
    DateTime CreatedUtc,
    DateTime? PaidUtc,
    DateTime? CanceledUtc);

/// <summary>
/// A line of an order.
/// </summary>
public sealed record ApiOrderLine(
    string Id,
    string ProductId,
    string VariantId,
    string Title,
    string VariantTitle,
    string Sku,
    int Quantity,
    ApiMoney UnitPrice,
    ApiMoney Total,
    IReadOnlyList<ApiOrderLineAddOn> AddOns,
    DateTime? ShipsFromUtc);

/// <summary>
/// An add-on the buyer chose for a line.
/// </summary>
public sealed record ApiOrderLineAddOn(string Id, string Label, string Value, ApiMoney Price);

/// <summary>
/// How the order goes: the rate it shipped at, or the location it's picked up from.
/// </summary>
public sealed record ApiOrderShipping(string Name, string Description, ApiMoney Amount, bool IsPickup, string LocationId);

/// <summary>
/// A discount the order got.
/// </summary>
public sealed record ApiOrderDiscount(string Code, string Description, ApiMoney Amount, string Target);

/// <summary>
/// A balance that paid toward the order: a gift card, store credit, points.
/// </summary>
public sealed record ApiOrderTender(string Provider, string Reference, string Description, ApiMoney Amount);

/// <summary>
/// Money given back.
/// </summary>
public sealed record ApiOrderRefund(string Id, ApiMoney Amount, string Reason, DateTime CreatedUtc);

/// <summary>
/// What the order comes to.
/// </summary>
public sealed record ApiOrderTotals(ApiMoney Subtotal, ApiMoney Discount, ApiMoney Shipping, ApiMoney Tax, ApiMoney Total, ApiMoney Refunded);

/// <summary>
/// A return the buyer asked for.
/// </summary>
/// <param name="Resolution"><c>refund</c> or <c>exchange</c>.</param>
/// <param name="Status">Where the return is: requested, approved, received, and how it closed.</param>
/// <param name="StoreMessage">What the store said, when it approved or turned it down.</param>
public sealed record ApiOrderReturn(
    string Id,
    string Number,
    string OrderId,
    string Status,
    string Resolution,
    string ExchangeFor,
    string Comment,
    string StoreMessage,
    IReadOnlyList<ApiOrderReturnLine> Lines,
    ApiMoney Refunded,
    DateTime CreatedUtc,
    DateTime? ClosedUtc);

/// <summary>
/// A line being returned.
/// </summary>
public sealed record ApiOrderReturnLine(string LineId, string Title, string Sku, int Quantity, string Reason);

/// <summary>
/// What can still be returned from an order, and why it may be returned.
/// </summary>
/// <param name="Lines">Each line that can be returned, and how many of it.</param>
/// <param name="Reasons">The reasons the store asks a buyer to pick from.</param>
/// <param name="AllowExchanges">Whether the buyer can ask for an exchange instead of a refund.</param>
/// <param name="WindowDays">How many days after it was paid an order can be returned.</param>
public sealed record ApiReturnable(IReadOnlyList<ApiReturnableLine> Lines, IReadOnlyList<string> Reasons, bool AllowExchanges, int? WindowDays);

/// <summary>
/// A line that can be returned, and how many of it.
/// </summary>
public sealed record ApiReturnableLine(string LineId, string Title, string Sku, int Quantity);

/// <summary>
/// A return to ask for.
/// </summary>
public sealed class ApiReturnRequest
{
    public List<ApiReturnRequestLine> Lines { get; set; } = [];

    /// <summary>
    /// <c>refund</c> or <c>exchange</c>.
    /// </summary>
    public string Resolution { get; set; }

    /// <summary>
    /// What the buyer would like instead, for an exchange.
    /// </summary>
    public string ExchangeFor { get; set; }

    public string Comment { get; set; }
}

/// <summary>
/// A line to return.
/// </summary>
public sealed class ApiReturnRequestLine
{
    public string LineId { get; set; }

    public int Quantity { get; set; }

    public string Reason { get; set; }
}

internal static class OrderApiMapper
{
    public static ApiOrderSummary ToSummary(Order order, bool isOwed)
        => new(
            order.ItemId,
            order.OrderNumber,
            ToCamel(order.Status.ToString()),
            order.FulfillmentStatus is { } fulfillment ? ToCamel(fulfillment.ToString()) : null,
            isOwed,
            order.ItemCount,
            ApiMoney.From(order.Total ?? order.Subtotal, order.Currency),
            order.CustomerName,
            order.CustomerEmail,
            order.CreatedUtc,
            order.PaidUtc);

    public static ApiOrder ToApi(Order order, bool isOwed, string payUrl)
    {
        var currency = order.Currency;

        return new ApiOrder(
            order.ItemId,
            order.OrderNumber,
            ToCamel(order.Status.ToString()),
            order.FulfillmentStatus is { } fulfillment ? ToCamel(fulfillment.ToString()) : null,
            ToCamel(order.RefundStatus.ToString()),
            isOwed,
            currency,
            order.CustomerId,
            order.CustomerName,
            order.CustomerEmail,
            order.CustomerPhone,
            ApiAddress.From(order.BillingAddress),
            ApiAddress.From(order.ShippingAddress),
            order.Shipping is null ? null : new ApiOrderShipping(order.Shipping.Name, order.Shipping.Description, ApiMoney.From(order.Shipping.Amount, currency), order.Shipping.IsPickup, order.Shipping.LocationId),
            order.Lines.Select(line => new ApiOrderLine(
                line.LineId,
                line.ContentItemId,
                line.VariantId,
                line.Title,
                line.VariantTitle,
                line.Sku,
                line.Quantity,
                ApiMoney.From(line.UnitPrice, currency),
                ApiMoney.From(line.LineTotal, currency),
                (line.AddOns ?? []).Select(addOn => new ApiOrderLineAddOn(addOn.AddOnId, addOn.Label, addOn.Value, ApiMoney.From(addOn.Price, currency))).ToList(),
                line.ShipsFromUtc)).ToList(),
            (order.Discounts ?? []).Select(discount => new ApiOrderDiscount(discount.Code, discount.Description, ApiMoney.From(discount.Amount, currency), ToCamel(discount.Target.ToString()))).ToList(),
            (order.Tenders ?? []).Select(tender => new ApiOrderTender(tender.ProviderKey, tender.Reference, tender.Description, ApiMoney.From(tender.Amount, currency))).ToList(),
            (order.Refunds ?? []).Select(refund => new ApiOrderRefund(refund.RefundId, ApiMoney.From(refund.Amount, refund.Currency ?? currency), refund.Reason, refund.CreatedUtc)).ToList(),
            new ApiOrderTotals(
                ApiMoney.From(order.Subtotal, currency),
                ApiMoney.From(order.DiscountTotal, currency),
                ApiMoney.From(order.Shipping?.Amount ?? 0m, currency),
                ApiMoney.From(order.TaxAmount ?? 0m, currency),
                ApiMoney.From(order.Total ?? order.Subtotal, currency),
                ApiMoney.From(order.RefundedAmount, currency)),
            payUrl,
            order.CreatedUtc,
            order.PaidUtc,
            order.CanceledUtc);
    }

    public static ApiOrderReturn ToApi(OrderReturn orderReturn)
        => new(
            orderReturn.ItemId,
            orderReturn.Number,
            orderReturn.OrderId,
            ToCamel(orderReturn.Status.ToString()),
            ToCamel(orderReturn.Resolution.ToString()),
            orderReturn.ExchangeFor,
            orderReturn.Comment,
            orderReturn.StoreMessage,
            orderReturn.Lines.Select(line => new ApiOrderReturnLine(line.LineId, line.Title, line.Sku, line.Quantity, line.Reason)).ToList(),
            ApiMoney.From(orderReturn.RefundedAmount, orderReturn.Currency),
            orderReturn.CreatedUtc,
            orderReturn.ClosedUtc);

    public static string ToCamel(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
