namespace CrestApps.OrchardCore.Orders.Core;

/// <summary>
/// Names shared by the Orders module.
/// </summary>
public static class OrderConstants
{
    /// <summary>
    /// The YesSql collection orders are stored in.
    /// </summary>
    public const string CollectionName = "Orders";

    /// <summary>
    /// The key of the concealed checkout step that carries an order's lines.
    /// </summary>
    public const string CheckoutStepKey = "Order";

    /// <summary>
    /// The site settings group of the Orders settings.
    /// </summary>
    public const string SettingsGroupId = "orders";

    public static class Features
    {
        public const string Orders = "CrestApps.OrchardCore.Orders";
    }

    public static class RouteNames
    {
        public const string MyOrders = "MyOrders";

        public const string MyOrder = "MyOrder";
    }
}
