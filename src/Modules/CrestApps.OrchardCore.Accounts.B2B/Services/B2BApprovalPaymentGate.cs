using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// Refuses to take payment for an account buyer's order that needs an account admin's approval it doesn't have, however
/// the buyer got to paying.
/// </summary>
/// <remarks>
/// For an account with a monthly budget, the check runs under the account's lock, and an order that passes holds its
/// total against the budget (<see cref="Order.BudgetHold"/>), committed before the lock is let go. Two of the account's
/// buyers paying at the same moment take turns, and the second one's check counts the first one's order.
/// </remarks>
public sealed class B2BApprovalPaymentGate : ICheckoutPaymentGate
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private readonly B2BCheckoutStepService _step;
    private readonly B2BApprovalService _approvals;
    private readonly IOrderStore _orders;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    public B2BApprovalPaymentGate(
        B2BCheckoutStepService step,
        B2BApprovalService approvals,
        IOrderStore orders,
        IDistributedLock distributedLock,
        ISession session,
        IStringLocalizer<B2BApprovalPaymentGate> stringLocalizer)
    {
        _step = step;
        _approvals = approvals;
        _orders = orders;
        _distributedLock = distributedLock;
        _session = session;
        S = stringLocalizer;
    }

    public async Task BeginningAsync(CheckoutPaymentBeginningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Flow.Session is not CheckoutSession session ||
            !string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            !session.TryGet<CheckoutInvoice>(out var invoice) ||
            await _orders.FindByIdAsync(session.ReferenceId, cancellationToken) is not { } order)
        {
            return;
        }

        if (await _step.GetB2BAccountAsync(session, cancellationToken) is not { MonthlyBudget: not null } account)
        {
            await RefuseWithoutApprovalAsync(context, session, order, invoice, cancellationToken);

            return;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(B2BApprovalService.GetBudgetLockKey(account.AccountId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            context.Refuse(S["Another order for {0} is being paid right now. Please try again in a moment.", account.Name]);

            return;
        }

        await using (locker)
        {
            if (await RefuseWithoutApprovalAsync(context, session, order, invoice, cancellationToken))
            {
                return;
            }

            await _approvals.HoldBudgetAsync(order, invoice.GrandTotal, invoice.Currency, cancellationToken);

            // Committed while the lock is held, so the account's next payment counts this order.
            await _session.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<bool> RefuseWithoutApprovalAsync(CheckoutPaymentBeginningContext context, CheckoutSession session, Order order, CheckoutInvoice invoice, CancellationToken cancellationToken)
    {
        if (await _step.GetApprovalReasonAsync(session, order, invoice, cancellationToken) is not { } reason)
        {
            return false;
        }

        context.Refuse(S["This order needs approval first: {0}. Ask for it in the Business account step.", reason]);

        return true;
    }
}
