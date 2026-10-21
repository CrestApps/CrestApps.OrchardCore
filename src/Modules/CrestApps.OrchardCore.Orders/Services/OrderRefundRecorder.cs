using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Records each refund of an order's money on the order, wherever it was started: from the order's page, the payments
/// page, or the order's Pay Later transaction.
/// </summary>
internal sealed class OrderRefundRecorder : IPaymentRefundHandler
{
    private readonly IOrderStore _orderStore;
    private readonly IOrderService _orderService;
    private readonly OrderOperationScope _scope;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ICheckoutSessionStore _sessionStore;

    internal readonly IStringLocalizer S;

    public OrderRefundRecorder(
        IOrderStore orderStore,
        IOrderService orderService,
        OrderOperationScope scope,
        IServiceProvider serviceProvider,
        IClock clock,
        IStringLocalizer<OrderRefundRecorder> stringLocalizer,
        ICheckoutSessionStore sessionStore)
    {
        _orderStore = orderStore;
        _orderService = orderService;
        _scope = scope;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _sessionStore = sessionStore;
        S = stringLocalizer;
    }

    public async Task RefundSucceededAsync(PaymentRefund refund, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refund);

        var order = await FindOrderAsync(refund, cancellationToken);

        if (order is null)
        {
            return;
        }

        var amount = Money.Round(refund.RefundGrossAmount, refund.Currency ?? order.Currency);
        var recorded = await _orderService.RecordRefundAsync(order, new OrderRefund
        {
            RefundId = refund.ItemId,
            Amount = amount,
            Currency = refund.Currency ?? order.Currency,
            Reason = refund.Reason,
            CreatedUtc = _clock.UtcNow,
        }, _scope.Restock, cancellationToken);

        if (!recorded)
        {
            return;
        }

        var formatted = ProductPriceFormatter.Format(amount, order.Currency);

        await _orderService.AddNoteAsync(
            order,
            string.IsNullOrWhiteSpace(refund.Reason) ? S["Refunded {0}.", formatted] : S["Refunded {0}: {1}", formatted, refund.Reason],
            OrderNoteKind.Event,
            _scope.ActorName,
            cancellationToken);
    }

    // The refund names its checkout: an order's own, or a pay link's for the order's Pay Later transaction.
    private async Task<Order> FindOrderAsync(PaymentRefund refund, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refund.SessionId))
        {
            return null;
        }

        var found = await _sessionStore.GetManyAsync([refund.SessionId], cancellationToken);
        var session = found.Count > 0 ? found[0] : null;

        if (session is null || string.IsNullOrEmpty(session.ReferenceId))
        {
            return null;
        }

        if (string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal))
        {
            return await _orderStore.FindByIdAsync(session.ReferenceId, cancellationToken);
        }

        if (string.Equals(session.ReferenceType, TransactionsConstants.ReferenceTypes.Transaction, StringComparison.OrdinalIgnoreCase) &&
            _serviceProvider.GetService<ITransactionStore>() is { } transactions &&
            await transactions.FindByIdAsync(session.ReferenceId, cancellationToken) is { } transaction &&
            string.Equals(transaction.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(transaction.ReferenceId))
        {
            return await _orderStore.FindByIdAsync(transaction.ReferenceId, cancellationToken);
        }

        return null;
    }
}
