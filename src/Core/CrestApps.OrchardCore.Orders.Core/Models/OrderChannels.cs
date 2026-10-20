namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// Where an order was made, for reports and for what its checkout asks.
/// </summary>
public static class OrderChannels
{
    /// <summary>
    /// The storefront, a headless storefront or an app: the buyer checks out themselves.
    /// </summary>
    public const string Online = "online";

    /// <summary>
    /// A store's register: staff ring it up and the buyer pays at the counter and takes it.
    /// </summary>
    public const string PointOfSale = "pos";

    /// <summary>
    /// The admin: staff make the order and send it to the buyer to pay.
    /// </summary>
    public const string Draft = "draft";

    /// <summary>
    /// Returns an order's channel, online for orders made before channels were recorded.
    /// </summary>
    public static string Of(Order order)
        => string.IsNullOrEmpty(order?.Channel) ? Online : order.Channel;

    /// <summary>
    /// Returns whether the order is sold over a counter: the buyer takes it, so nothing is shipped and no contact
    /// details are asked for.
    /// </summary>
    public static bool IsCounterSale(Order order)
        => string.Equals(order?.Channel, PointOfSale, StringComparison.Ordinal);
}
