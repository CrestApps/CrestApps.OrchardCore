using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Signed, expiring links that open one order without signing in, for guests: in the confirmation email and from
/// Find my order.
/// </summary>
public interface IOrderLinkService
{
    /// <summary>
    /// The link to the order, or <see langword="null"/> when no absolute address can be built (no site base URL and no
    /// request).
    /// </summary>
    Task<string> CreateUrlAsync(Order order);

    /// <summary>
    /// The link that pays the order, or <see langword="null"/> when no absolute address can be built (no site base URL
    /// and no request).
    /// </summary>
    Task<string> CreatePayUrlAsync(Order order);

    /// <summary>
    /// The id of the order a link's token names, or <see langword="null"/> when the token is invalid or expired.
    /// </summary>
    string GetOrderId(string token);
}
