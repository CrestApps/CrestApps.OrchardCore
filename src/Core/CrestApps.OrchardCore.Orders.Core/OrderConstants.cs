namespace CrestApps.OrchardCore.Orders.Core;

/// <summary>
/// Names shared by the Orders module.
/// </summary>
public static class OrderConstants
{
    /// <summary>
    /// The display types an order is built in for the sections other modules add to its pages.
    /// </summary>
    public static class DisplayTypes
    {
        /// <summary>
        /// The order's admin page, also a customer's own order under My Orders.
        /// </summary>
        public const string DetailAdmin = "DetailAdmin";

        /// <summary>
        /// The page a guest opens from the order's link.
        /// </summary>
        public const string Detail = "Detail";
    }

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

    public const string ReturnsSettingsGroupId = "returns";

    /// <summary>
    /// The rate-limit group of Find my order, which anyone can post to.
    /// </summary>
    public const string FindRateLimitGroup = "find-order";

    public static class Features
    {
        public const string Orders = "CrestApps.OrchardCore.Orders";

        public const string Returns = "CrestApps.OrchardCore.Orders.Returns";
    }

    public static class RouteNames
    {
        public const string MyOrders = "MyOrders";

        public const string MyOrder = "MyOrder";

        public const string View = "OrdersView";

        public const string Find = "OrdersFind";
    }
}
