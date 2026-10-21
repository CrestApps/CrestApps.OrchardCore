using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Refunds for other modules, through the order's own refund operation.
/// </summary>
internal sealed class OrderRefunder : IOrderRefunder
{
    private readonly OrderOperationsService _operations;

    public OrderRefunder(OrderOperationsService operations)
    {
        _operations = operations;
    }

    public async Task<(decimal Refundable, decimal Outstanding)> GetMoneyAsync(Order order, CancellationToken cancellationToken = default)
    {
        var money = await _operations.GetMoneyAsync(order, cancellationToken);

        return (money.Refundable, money.Outstanding);
    }

    public async Task<(decimal Refunded, IReadOnlyList<string> Errors)> RefundAsync(Order order, decimal amount, string reason, string actorName, CancellationToken cancellationToken = default)
    {
        var result = await _operations.RefundAsync(order, amount, reason, restock: false, actorName, cancellationToken);

        return (result.Refunded, [.. result.Errors]);
    }
}

/// <summary>
/// Requests the store answers before the buyer pays, as quotes.
/// </summary>
internal sealed class OrderRequestService : IOrderRequestService
{
    private readonly QuoteService _quotes;

    public OrderRequestService(QuoteService quotes)
    {
        _quotes = quotes;
    }

    public Task<(Order Order, IReadOnlyList<string> Problems)> RequestAsync(ClaimsPrincipal user, string message, CancellationToken cancellationToken = default)
        => _quotes.RequestAsync(user, message, purchaseOrderNumber: null, requireQuoteRequests: false, cancellationToken);
}
