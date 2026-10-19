using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Cancels unpaid orders that nothing can pay for any more: their checkout was canceled, failed or expired without the
/// order hearing of it, or is gone, or was never started. Orders whose checkout is still open are left to it.
/// </summary>
public sealed class AbandonedOrderSweeper
{
    internal const int PageSize = 50;
    internal const int MaxOrdersPerRun = 500;

    // A draft becomes payable a moment after it is made, once its checkout starts. One that never got a checkout in
    // this long never will.
    internal static readonly TimeSpan DraftGrace = TimeSpan.FromHours(1);

    private readonly IOrderStore _orderStore;
    private readonly IOrderService _orderService;
    private readonly ICheckoutSessionStore _sessionStore;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public AbandonedOrderSweeper(
        IOrderStore orderStore,
        IOrderService orderService,
        ICheckoutSessionStore sessionStore,
        IClock clock,
        ILogger<AbandonedOrderSweeper> logger)
    {
        _orderStore = orderStore;
        _orderService = orderService;
        _sessionStore = sessionStore;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Cancels a batch of abandoned orders, and returns how many.
    /// </summary>
    public async Task<int> SweepAsync(CancellationToken cancellationToken = default)
    {
        var query = new OrderQuery
        {
            Statuses = [OrderStatus.Draft, OrderStatus.AwaitingPayment],
            CreatedBefore = _clock.UtcNow - DraftGrace,
        };

        var canceled = 0;
        var visited = 0;
        var skip = 0;

        while (visited < MaxOrdersPerRun && !cancellationToken.IsCancellationRequested)
        {
            var (_, orders) = await _orderStore.PageAsync(query, skip, PageSize, cancellationToken);

            if (orders.Count == 0)
            {
                break;
            }

            var canceledInPage = 0;

            foreach (var order in orders)
            {
                visited++;

                if (await GetReasonAsync(order, cancellationToken) is { } reason &&
                    await _orderService.CancelAsync(order, reason, cancellationToken))
                {
                    canceledInPage++;
                }
            }

            canceled += canceledInPage;

            // Canceled orders drop out of the query; the ones still open are stepped over.
            skip += orders.Count - canceledInPage;

            if (orders.Count < PageSize)
            {
                break;
            }
        }

        if (canceled > 0 && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Canceled {Count} abandoned orders.", canceled);
        }

        return canceled;
    }

    private async Task<OrderCancelReason?> GetReasonAsync(Order order, CancellationToken cancellationToken)
    {
        // An order the store made for the buyer to pay waits for them; the store cancels it if it must.
        if (order.Invoice is not null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(order.CheckoutSessionId))
        {
            return OrderCancelReason.Abandoned;
        }

        var session = await _sessionStore.GetAsync(order.CheckoutSessionId, cancellationToken);

        return session?.Status switch
        {
            null => OrderCancelReason.Abandoned,
            CheckoutSessionStatus.Failed => OrderCancelReason.PaymentFailed,
            CheckoutSessionStatus.Canceled => OrderCancelReason.CheckoutCanceled,
            CheckoutSessionStatus.Expired => OrderCancelReason.CheckoutExpired,

            // Open checkouts can still be paid, and a completed one pays the order through the reconciliation sweep.
            _ => null,
        };
    }
}
