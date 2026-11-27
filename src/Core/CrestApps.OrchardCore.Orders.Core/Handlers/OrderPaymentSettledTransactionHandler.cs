using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Core.Handlers;

/// <summary>
/// When the transaction for an order paid later is paid, records that the money arrived and lets the order's handlers
/// deliver what they held back, such as downloads and memberships.
/// </summary>
public sealed class OrderPaymentSettledTransactionHandler : TransactionEventHandlerBase
{
    private readonly IOrderStore _orderStore;
    private readonly ITransactionStore _transactionStore;
    private readonly IEnumerable<IOrderEventHandler> _orderHandlers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderPaymentSettledTransactionHandler"/> class.
    /// </summary>
    public OrderPaymentSettledTransactionHandler(
        IOrderStore orderStore,
        ITransactionStore transactionStore,
        IEnumerable<IOrderEventHandler> orderHandlers,
        IClock clock,
        ILogger<OrderPaymentSettledTransactionHandler> logger)
    {
        _orderStore = orderStore;
        _transactionStore = transactionStore;
        _orderHandlers = orderHandlers;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
    {
        if (current != TransactionStatus.Paid ||
            !string.Equals(transaction?.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(transaction.ReferenceId))
        {
            return;
        }

        var order = await _orderStore.FindByIdAsync(transaction.ReferenceId, cancellationToken);

        if (order is null || order.IsPaymentCollected || await HasOtherUnsettledAsync(transaction, cancellationToken))
        {
            return;
        }

        order.PaymentSettledUtc = _clock.UtcNow;
        order.ModifiedUtc = order.PaymentSettledUtc.Value;

        await _orderStore.UpdateAsync(order, cancellationToken);

        await _orderHandlers.InvokeAsync((handler, context) => handler.PaymentSettledAsync(context, cancellationToken), new OrderPaidContext(order), _logger);
    }

    // An order paid later can owe more than one balance, for example a one-time purchase and the first period of a plan
    // bought with it. What the order held back is delivered only once none of them is still owed, so paying the plan's
    // small balance doesn't release the large purchase. Later cycles of a plan bill new periods and don't hold the order.
    private async Task<bool> HasOtherUnsettledAsync(Transaction paid, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(paid.CheckoutSessionId))
        {
            return false;
        }

        var transactions = await _transactionStore.GetByCheckoutSessionAsync(paid.CheckoutSessionId, cancellationToken) ?? [];

        return transactions.Any(candidate =>
            !string.Equals(candidate.ItemId, paid.ItemId, StringComparison.Ordinal) &&
            string.Equals(candidate.ReferenceType, paid.ReferenceType, StringComparison.Ordinal) &&
            string.Equals(candidate.ReferenceId, paid.ReferenceId, StringComparison.Ordinal) &&
            (candidate.Recurrence?.CycleNumber ?? 1) <= 1 &&
            candidate.Status is not (TransactionStatus.Paid or TransactionStatus.Refunded or TransactionStatus.Canceled));
    }
}
