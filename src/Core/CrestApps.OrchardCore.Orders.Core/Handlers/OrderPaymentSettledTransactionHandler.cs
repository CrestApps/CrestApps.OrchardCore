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
    private readonly IEnumerable<IOrderEventHandler> _orderHandlers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderPaymentSettledTransactionHandler"/> class.
    /// </summary>
    public OrderPaymentSettledTransactionHandler(
        IOrderStore orderStore,
        IEnumerable<IOrderEventHandler> orderHandlers,
        IClock clock,
        ILogger<OrderPaymentSettledTransactionHandler> logger)
    {
        _orderStore = orderStore;
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

        if (order is null || order.IsPaymentCollected)
        {
            return;
        }

        order.PaymentSettledUtc = _clock.UtcNow;
        order.ModifiedUtc = order.PaymentSettledUtc.Value;

        await _orderStore.UpdateAsync(order, cancellationToken);

        await _orderHandlers.InvokeAsync((handler, context) => handler.PaymentSettledAsync(context, cancellationToken), new OrderPaidContext(order), _logger);
    }
}
