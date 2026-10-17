using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Removes who an order was for, the guest contact of the transactions paying for it, and the buyer's details from the
/// checkout sessions behind both. The lines, totals and dates stay for the store's accounts.
/// </summary>
public sealed class OrderAnonymizer
{
    private const int PageSize = 50;

    private readonly IOrderStore _orderStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    public OrderAnonymizer(IOrderStore orderStore, IServiceProvider serviceProvider, IClock clock)
    {
        _orderStore = orderStore;
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    /// <summary>
    /// Anonymizes every order the query finds, and returns how many. The query must leave out anonymized orders, so
    /// each page starts from the beginning again.
    /// </summary>
    public async Task<int> AnonymizeAsync(OrderQuery query, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        query.NotAnonymized = true;

        var count = 0;

        while (count < limit)
        {
            var (_, orders) = await _orderStore.PageAsync(query, 0, Math.Min(PageSize, limit - count), cancellationToken);

            if (orders.Count == 0)
            {
                break;
            }

            foreach (var order in orders)
            {
                await AnonymizeAsync(order, cancellationToken);
                count++;
            }

            if (orders.Count < PageSize)
            {
                break;
            }
        }

        return count;
    }

    public async Task AnonymizeAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        order.Anonymize(_clock.UtcNow);

        await _orderStore.UpdateAsync(order, cancellationToken);

        // Every attempt to pay holds what the buyer typed, not only the session the order names.
        var sessions = _serviceProvider.GetService<ICheckoutSessionAnonymizer>();

        if (sessions is not null)
        {
            await sessions.AnonymizeAsync(order.CheckoutSessionId, cancellationToken);
            await sessions.AnonymizeByReferenceAsync(CheckoutReferenceTypes.Order, order.ItemId, cancellationToken);
        }

        if (_serviceProvider.GetService<ITransactionManager>() is not { } transactions)
        {
            return;
        }

        var result = await transactions.PageAsync(1, PageSize, new TransactionQuery
        {
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = order.ItemId,
        }, cancellationToken);

        foreach (var transaction in result.Entries)
        {
            // A balance left on the order is paid through a checkout of its own transaction.
            if (sessions is not null)
            {
                await sessions.AnonymizeByReferenceAsync(TransactionsConstants.ReferenceTypes.Transaction, transaction.ItemId, cancellationToken);
            }

            if (transaction.GuestContactName is null && transaction.GuestContactEmail is null)
            {
                continue;
            }

            transaction.GuestContactName = null;
            transaction.GuestContactEmail = null;

            await transactions.UpdateAsync(transaction, data: null, cancellationToken);
        }
    }
}
