using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Gives an order's refund to its buyer as store credit instead of back to how they paid. The Store Credit feature
/// implements it; without it, refunds only go back to how the buyer paid.
/// </summary>
public interface IOrderStoreCreditRefunder
{
    /// <summary>
    /// Returns whether the order's buyer can be given store credit: they have an account to spend it from.
    /// </summary>
    Task<bool> CanRefundAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds <paramref name="amount"/> to the buyer's store credit in the order's currency. Returns whether it was
    /// added.
    /// </summary>
    Task<bool> RefundAsync(Order order, decimal amount, string reason, string actor, CancellationToken cancellationToken = default);
}
