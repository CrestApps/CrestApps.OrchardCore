using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// The records an email about an order carries, which the store's template of it reads.
/// </summary>
public static class OrderEmailModels
{
    /// <summary>
    /// The name the store's template reads the order under, as <c>{{ Order }}</c>.
    /// </summary>
    public const string OrderName = "Order";

    /// <summary>
    /// Returns the models of an email about <paramref name="order"/>: the order as <c>{{ Order }}</c>, or none.
    /// </summary>
    /// <param name="order">The order, or <see langword="null"/>.</param>
    public static IReadOnlyDictionary<string, object> For(Order order)
        => order is null
            ? new Dictionary<string, object>(StringComparer.Ordinal)
            : new Dictionary<string, object>(StringComparer.Ordinal) { [OrderName] = order };
}
