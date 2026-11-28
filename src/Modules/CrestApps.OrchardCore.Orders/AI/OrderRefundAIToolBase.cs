using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// What <c>previewRefund</c> and <c>refundOrder</c> share: the money rules of the owner's review (F23), checked the same
/// way by both, so a preview is issued only for a refund <c>refundOrder</c> would accept, and the fingerprint of the
/// refund, which ties a preview's token and an idempotency key to one refund.
/// </summary>
internal abstract class OrderRefundAIToolBase : OrdersAIToolBase
{
    /// <summary>
    /// The cache-key purpose of the refund preview tokens (<see cref="AIActionPreviewTokens"/>), so they never mix with
    /// another module's.
    /// </summary>
    protected const string PreviewPurpose = "orders-refund-preview";

    /// <summary>
    /// Checks a refund's arguments against the order, with the order page's limits and the review's rules: the amount
    /// is more than zero and fits the currency's decimal places, the currency is the order's, everything still
    /// refundable needs <c>refundAll</c> with that exact amount, store credit only when the order allows it, and a return
    /// must be an approved or received return of the order, without restock or store credit.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="order">The order.</param>
    /// <param name="refused">How a refusal starts, such as "Nothing was refunded".</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The checked refund, or the sentence that says why it is refused.</returns>
    protected static async Task<(OrderRefundRequest Request, string Error)> ReadRefundAsync(
        IServiceProvider services,
        AIFunctionArguments arguments,
        Order order,
        string refused,
        CancellationToken cancellationToken)
    {
        if (!TryReadDecimal(arguments, "amount", out var amount) || amount is not > 0m)
        {
            return (null, $"{refused}: give the amount to give back, more than zero.");
        }

        var currency = ReadString(arguments, "currency");

        if (!string.Equals(currency, order.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return (null, $"{refused}: order {order.OrderNumber} was paid in {order.Currency}; give the amount in {order.Currency}.");
        }

        if (Money.Round(amount.Value, order.Currency) != amount.Value)
        {
            return (null, $"{refused}: {amount.Value.ToString(CultureInfo.InvariantCulture)} has more decimal places than {order.Currency} allows.");
        }

        var refundAll = ReadBool(arguments, "refundAll") == true;
        var asStoreCredit = ReadBool(arguments, "asStoreCredit") == true;
        var restock = ReadBool(arguments, "restock") == true;
        var money = await services.GetRequiredService<IOrderOperationsService>().GetMoneyAsync(order, cancellationToken);

        if (money.Refundable <= 0m)
        {
            return (null, $"{refused}: nothing paid for order {order.OrderNumber} can be refunded.");
        }

        if (refundAll && amount.Value != money.Refundable)
        {
            return (null, $"{refused}: everything still refundable for order {order.OrderNumber} is {Format(money.Refundable, order.Currency)}. Give that amount with refundAll, or a smaller amount without it.");
        }

        if (!refundAll && amount.Value == money.Refundable)
        {
            return (null, $"{refused}: {Format(amount.Value, order.Currency)} is everything still refundable for order {order.OrderNumber}. Set refundAll to true to give it all back.");
        }

        if (!refundAll && amount.Value > money.Refundable)
        {
            return (null, $"{refused}: only {Format(money.Refundable, order.Currency)} can still be refunded for order {order.OrderNumber}.");
        }

        if (asStoreCredit && !money.CanRefundAsStoreCredit)
        {
            return (null, $"{refused}: this order's refund can't be given as store credit. The buyer needs an account and the Store credit feature must be on.");
        }

        // A return's refund goes through the Returns page's own step, which refunds the order, closes the return and
        // emails the buyer.
        var returnId = ReadString(arguments, "returnId");
        OrderReturn orderReturn = null;
        OrderReturnService returnService = null;

        if (returnId is not null)
        {
            returnService = services.GetService<OrderReturnService>();
            var returnStore = services.GetService<IOrderReturnStore>();

            if (returnService is null || returnStore is null)
            {
                return (null, $"{refused}: returns are not turned on for this site (the Returns feature). Leave out returnId.");
            }

            if (restock || asStoreCredit)
            {
                return (null, $"{refused}: a return's refund goes back to how the buyer paid and puts nothing back into stock. Leave out restock and asStoreCredit, and use receiveOrderReturn to restock the returned items.");
            }

            orderReturn = await returnStore.FindByIdAsync(returnId, cancellationToken);

            if (orderReturn is null || !string.Equals(orderReturn.OrderId, order.ItemId, StringComparison.Ordinal))
            {
                return (null, $"{refused}: order {order.OrderNumber} has no return with the id '{returnId}'. Use listOrderReturns to find it.");
            }

            if (orderReturn.Status is not (OrderReturnStatus.Approved or OrderReturnStatus.Received))
            {
                return (null, $"{refused}: return {orderReturn.Number} is {ToCamel(orderReturn.Status.ToString())}; only an approved or received return can be refunded.");
            }
        }

        // Where the refund would go now, planned as the refund itself goes (IOrderRefundPlanner). It is part of the token's
        // fingerprint, so a refund made or a payment changed after the preview makes the token fail.
        var plan = services.GetService<IOrderRefundPlanner>() is { } planner
            ? await planner.PlanRefundAsync(order, amount.Value, asStoreCredit, cancellationToken)
            : null;

        return (new OrderRefundRequest(order, amount.Value, refundAll, asStoreCredit, restock, orderReturn, returnService, money, plan), null);
    }

    /// <summary>
    /// Gets who a preview's token belongs to: the tenant and the caller, so a token works only for the user it was issued
    /// to, on the site it was issued on.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <returns>The scope of the caller's preview tokens.</returns>
    protected static string GetPreviewScope(IServiceProvider services, ClaimsPrincipal caller)
        => $"{services.GetService<ShellSettings>()?.Name}|ai-tool:previewRefund|{caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.Identity?.Name}";
}

/// <summary>
/// A refund whose arguments passed the money rules.
/// </summary>
/// <param name="Order">The order.</param>
/// <param name="Amount">How much to give back, in the order's currency.</param>
/// <param name="RefundAll">Whether it is everything still refundable.</param>
/// <param name="AsStoreCredit">Whether it is given as store credit.</param>
/// <param name="Restock">Whether what the order sold goes back into stock.</param>
/// <param name="Return">The return it is for, or <see langword="null"/>.</param>
/// <param name="ReturnService">The Returns page's service, when it is for a return.</param>
/// <param name="Totals">What the order can still refund and still owes.</param>
/// <param name="Plan">Where the refund would go now, or <see langword="null"/> when it can't be planned.</param>
internal sealed record OrderRefundRequest(
    Order Order,
    decimal Amount,
    bool RefundAll,
    bool AsStoreCredit,
    bool Restock,
    OrderReturn Return,
    OrderReturnService ReturnService,
    OrderMoney Totals,
    OrderRefundPlan Plan)
{
    /// <summary>
    /// Gets the fingerprint of what was asked: the order, the amount, the currency and the options. An idempotency key is
    /// bound to it, so a retry of a refund that ran gets its first answer back even though the order changed since. The
    /// amount is written without trailing zeros, so 25.5 and 25.50 are the same refund.
    /// </summary>
    public string RequestFingerprint
        => string.Join('|', Order.ItemId, Invariant(Amount), Order.Currency, RefundAll, AsStoreCredit, Restock, Return?.ItemId);

    /// <summary>
    /// Gets the fingerprint a preview's token is bound to: what was asked, and the order's refund state when it was
    /// checked: what is still refundable, what was refunded so far, the return's refunded amount and status, and the
    /// payments the refund would go to, skip or not cover. A refund made, or a payment changed, after the preview changes
    /// it, so a second preview of the same refund can't be used once the first was, and a refund never goes anywhere
    /// other than the preview showed.
    /// </summary>
    public string Fingerprint
        => string.Join(
            '|',
            RequestFingerprint,
            Invariant(Totals?.Refundable ?? 0m),
            Invariant(Order.RefundedAmount),
            (Order.Refunds?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
            Return is null ? string.Empty : Invariant(Return.RefundedAmount),
            Return?.Status.ToString(),
            Describe(Plan?.Portions),
            Describe(Plan?.Skipped),
            Plan is null ? "unplanned" : Invariant(Plan.NotCovered));

    private static string Invariant(decimal amount)
        => amount.ToString("0.############################", CultureInfo.InvariantCulture);

    private static string Describe(List<OrderRefundPortion> portions)
        => portions is null
            ? string.Empty
            : string.Join(';', portions.Select(portion => $"{portion.Kind}:{portion.Description}:{Invariant(portion.Amount)}"));
}
