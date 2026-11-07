using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Services;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// What the store does to an order: refund it, cancel it, resend its confirmation, and read what can still be refunded
/// and what is still owed. The Orders feature provides it, so other modules, such as Point of Sale, act on orders
/// without using the Orders module's own classes.
/// </summary>
public interface IOrderOperationsService
{
    /// <summary>
    /// Returns whether money is still owed on a placed order, such as one paid by bank transfer before the money
    /// arrives.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when money is still owed on the order.</returns>
    Task<bool> IsOwedAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns what can still be refunded for the order, and what is still owed for it, such as through Pay Later or a
    /// bank transfer.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What can still be refunded and what is still owed.</returns>
    Task<OrderMoney> GetMoneyAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives back <paramref name="amount"/> of what was paid for the order, or everything still refundable, newest
    /// payment first. With <paramref name="restock"/>, what the order sold goes back into stock once everything is
    /// refunded.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="amount">The amount to refund, or <see langword="null"/> for everything still refundable.</param>
    /// <param name="reason">Why the order is refunded.</param>
    /// <param name="restock">Whether what the order sold goes back into stock.</param>
    /// <param name="actorName">Who refunds it, for the order's notes.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was refunded, and why anything couldn't be.</returns>
    Task<OrderOperationResult> RefundAsync(Order order, decimal? amount, string reason, bool restock, string actorName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives back <paramref name="amount"/> of what was paid for the order, or everything still refundable: newest
    /// payment first, or, with <paramref name="asStoreCredit"/>, as store credit for the buyer to spend later.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="amount">The amount to refund, or <see langword="null"/> for everything still refundable.</param>
    /// <param name="reason">Why the order is refunded.</param>
    /// <param name="restock">Whether what the order sold goes back into stock.</param>
    /// <param name="actorName">Who refunds it, for the order's notes.</param>
    /// <param name="asStoreCredit">Whether the refund is given as store credit.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was refunded, and why anything couldn't be.</returns>
    Task<OrderOperationResult> RefundAsync(Order order, decimal? amount, string reason, bool restock, string actorName, bool asStoreCredit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the order. A paid one can have everything refunded with it, and what it sold put back into stock; what
    /// is still owed through Pay Later is canceled, and an unpaid checkout is let go.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="note">Why the order is canceled.</param>
    /// <param name="refund">Whether everything paid is refunded.</param>
    /// <param name="restock">Whether what the order sold goes back into stock.</param>
    /// <param name="actorName">Who cancels it, for the order's notes.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>What was refunded, and why anything couldn't be done.</returns>
    Task<OrderOperationResult> CancelAsync(Order order, string note, bool refund, bool restock, string actorName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Emails the order's confirmation to the customer again.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="actorName">Who sends it, for the order's notes.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the confirmation was sent.</returns>
    Task<bool> ResendConfirmationAsync(Order order, string actorName, CancellationToken cancellationToken = default);
}
