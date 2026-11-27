using CrestApps.Core;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Refuses to take payment for an order that was already paid, or that another checkout is paying right now.
/// </summary>
/// <remarks>
/// An order's pay link opened on two devices, or in two tabs, starts two checkouts of the same order, and both could
/// take the money; the second checkout to complete found the order paid and kept its payment. The check runs under the
/// order's lock (<see cref="GetLockKey"/>), and a payment that passes leaves a <see cref="CheckoutPaymentHold"/> on the
/// order, committed before the lock is let go. The other checkout's payment is refused while the holder's payment may
/// still take the money, as <see cref="ICheckoutPaymentHoldService"/> decides.
/// </remarks>
public sealed class OrderPaymentGate : ICheckoutPaymentGate
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private readonly IOrderStore _orderStore;
    private readonly ICheckoutPaymentHoldService _holds;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderPaymentGate"/> class.
    /// </summary>
    /// <param name="orderStore">The order store.</param>
    /// <param name="holds">Tells whether another checkout's payment for the order may still take the money.</param>
    /// <param name="distributedLock">The lock each order is checked and held under.</param>
    /// <param name="session">The YesSql session the hold is committed with.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OrderPaymentGate(
        IOrderStore orderStore,
        ICheckoutPaymentHoldService holds,
        IDistributedLock distributedLock,
        ISession session,
        IStringLocalizer<OrderPaymentGate> stringLocalizer)
    {
        _orderStore = orderStore;
        _holds = holds;
        _distributedLock = distributedLock;
        _session = session;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns the key of the lock an order's payments are checked under.
    /// </summary>
    /// <param name="orderId">The order's id.</param>
    public static string GetLockKey(string orderId)
        => "ORDER_PAY_" + orderId;

    /// <inheritdoc/>
    public async Task BeginningAsync(CheckoutPaymentBeginningContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Flow.Session is not CheckoutSession session ||
            !string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(GetLockKey(session.ReferenceId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            context.Refuse(S["Another payment for this order is being made right now. Please try again in a moment."]);

            return;
        }

        await using (locker)
        {
            var order = await _orderStore.FindByIdAsync(session.ReferenceId, cancellationToken);

            if (order is null || order.Status == OrderStatus.Paid)
            {
                context.Refuse(S["This order was already paid. Open the order to see where it stands."]);

                return;
            }

            var current = order.TryGet<CheckoutPaymentHold>(out var hold) ? hold : null;

            if (await _holds.IsHeldByAnotherAsync(current, session.SessionId, cancellationToken: cancellationToken))
            {
                context.Refuse(S["A payment for this order was already started on another page and may still go through. Finish it there, or open the order again in a few minutes."]);

                return;
            }

            order.Put(_holds.Take(current, session.SessionId));

            await _orderStore.UpdateAsync(order, cancellationToken);

            // Committed while the lock is held, so the next checkout of this order sees the hold.
            await _session.SaveChangesAsync(cancellationToken);
        }
    }
}
