using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Accounts.B2B.Handlers;

/// <summary>
/// Records the account an order is placed for, when its buyer buys for one, so the account sees it and pays it on its
/// terms.
/// </summary>
public sealed class B2BOrderHandler : OrderEventHandlerBase
{
    private readonly B2BAccountService _accounts;

    public B2BOrderHandler(B2BAccountService accounts)
    {
        _accounts = accounts;
    }

    public override async Task CreatedAsync(OrderCreatedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var order = context.Order;

        if (string.IsNullOrEmpty(order.AccountId) && await _accounts.GetForUserAsync(order.UserId, cancellationToken) is { } account)
        {
            order.AccountId = account.AccountId;
        }
    }
}
