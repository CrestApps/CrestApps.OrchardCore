using CrestApps.OrchardCore.Carts.Core.Models;

namespace CrestApps.OrchardCore.Carts.Core.Services;

/// <summary>
/// Offers something to do with a cart besides checking out, such as asking for a quote; the cart page shows each as a
/// link under the Checkout button.
/// </summary>
public interface ICartActionProvider
{
    Task<IReadOnlyList<CartAction>> GetActionsAsync(CartPricing pricing, CancellationToken cancellationToken = default);
}

/// <summary>
/// A link the cart page offers: what it says, where it goes, and a line on what it does.
/// </summary>
public sealed record CartAction(string Label, string Url, string Description = null);
