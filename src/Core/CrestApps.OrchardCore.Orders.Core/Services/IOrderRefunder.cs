using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Refunds part of what was paid for an order, for modules that refund by their own rules, such as a booking canceled
/// under its cancellation policy. The Orders feature provides it.
/// </summary>
public interface IOrderRefunder
{
    /// <summary>
    /// Returns what can still be refunded for the order, and what is still owed on it.
    /// </summary>
    Task<(decimal Refundable, decimal Outstanding)> GetMoneyAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives back <paramref name="amount"/> of what was paid for the order, newest payment first, and records it on the
    /// order. Returns what was refunded and why anything couldn't be.
    /// </summary>
    Task<(decimal Refunded, IReadOnlyList<string> Errors)> RefundAsync(Order order, decimal amount, string reason, string actorName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Turns a signed-in buyer's cart into a request the store answers before the buyer pays, such as a booking the store
/// confirms first: a draft order with a quote the store sends, whose pay link the buyer then pays. The Orders feature
/// provides it.
/// </summary>
public interface IOrderRequestService
{
    /// <summary>
    /// Makes the request from the buyer's cart, and empties the cart. It's taken whether or not the store takes quote
    /// requests for other carts.
    /// </summary>
    Task<(Order Order, IReadOnlyList<string> Problems)> RequestAsync(ClaimsPrincipal user, string message, CancellationToken cancellationToken = default);
}
