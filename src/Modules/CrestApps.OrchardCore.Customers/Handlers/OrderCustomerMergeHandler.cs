using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Moves a merged guest's orders to the account's customer, and the transactions paying for them to the account, so
/// they show under My Orders and My Transactions.
/// </summary>
public sealed class OrderCustomerMergeHandler : ICustomerMergeHandler
{
    private const int PageSize = 50;

    private readonly IOrderStore _orderStore;
    private readonly IServiceProvider _serviceProvider;

    public OrderCustomerMergeHandler(IOrderStore orderStore, IServiceProvider serviceProvider)
    {
        _orderStore = orderStore;
        _serviceProvider = serviceProvider;
    }

    public async Task MergingAsync(CustomerMergeContext context, CancellationToken cancellationToken = default)
    {
        var transactions = _serviceProvider.GetService<ITransactionManager>();

        while (true)
        {
            // Each page moves its orders off the guest, so the next page starts from the beginning again.
            var (_, orders) = await _orderStore.PageAsync(new OrderQuery { CustomerId = context.Guest.ItemId }, 0, PageSize, cancellationToken);

            if (orders.Count == 0)
            {
                break;
            }

            foreach (var order in orders)
            {
                order.CustomerId = context.Into.ItemId;
                order.UserId ??= context.UserId;

                await _orderStore.UpdateAsync(order, cancellationToken);

                if (transactions is not null)
                {
                    await MoveTransactionsAsync(transactions, order.ItemId, context.UserId, cancellationToken);
                }
            }

            if (orders.Count < PageSize)
            {
                break;
            }
        }
    }

    private static async Task MoveTransactionsAsync(ITransactionManager transactions, string orderId, string userId, CancellationToken cancellationToken)
    {
        var result = await transactions.PageAsync(1, PageSize, new TransactionQuery
        {
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = orderId,
            OwnerKind = CustomerOwnerKind.Guest,
        }, cancellationToken);

        foreach (var transaction in result.Entries)
        {
            transaction.OwnerKind = CustomerOwnerKind.Authenticated;
            transaction.OwnerId = userId;

            await transactions.UpdateAsync(transaction, data: null, cancellationToken);
        }
    }
}
