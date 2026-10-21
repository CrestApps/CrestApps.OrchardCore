using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Decides whether an order can be paid, and finds the link that pays it: the order's own pay page for an order the
/// store sent the buyer to pay, or the link that pays what is still owed on it when Transactions is on.
/// </summary>
public sealed class OrderPayLinks
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    public OrderPayLinks(
        IServiceProvider serviceProvider,
        IClock clock)
    {
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    /// <summary>
    /// Returns whether the order is one the store sent the buyer to pay, that its pay page can open a checkout for: it
    /// has an invoice, isn't paid or canceled, and its quote and approval let it be paid.
    /// </summary>
    public bool CanPayByLink(Order order)
        => order?.Invoice is not null &&
            order.Status is OrderStatus.Draft or OrderStatus.AwaitingPayment &&
            QuoteService.CanPay(order, _clock.UtcNow);

    /// <summary>
    /// Returns the link that pays the order, or <see langword="null"/> when it can't be paid.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="orderPayUrl">The address of the order's own pay page, used for an order the store sent the buyer to
    /// pay; <see langword="null"/> when the caller has no such page, and only what is owed through a transaction is
    /// linked.</param>
    public async Task<string> GetPayUrlAsync(Order order, string orderPayUrl = null)
    {
        if (order is null || order.Status == OrderStatus.Canceled || !QuoteService.CanPay(order, _clock.UtcNow))
        {
            return null;
        }

        if (orderPayUrl is not null && CanPayByLink(order))
        {
            return orderPayUrl;
        }

        // Transactions is optional, so what is owed is only linked when it is on.
        if (_serviceProvider.GetService<ITransactionStore>() is not { } transactions ||
            _serviceProvider.GetService<ITransactionPayLinkService>() is not { } payLinks)
        {
            return null;
        }

        var result = await transactions.PageAsync(1, 1, new TransactionQuery
        {
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = order.ItemId,
            OutstandingOnly = true,
        });

        var outstanding = result.Entries.FirstOrDefault();

        return outstanding is null ? null : await payLinks.CreatePayUrlAsync(outstanding);
    }
}
