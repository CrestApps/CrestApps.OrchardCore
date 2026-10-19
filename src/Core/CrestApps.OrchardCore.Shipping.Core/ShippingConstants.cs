namespace CrestApps.OrchardCore.Shipping;

public static class ShippingConstants
{
    /// <summary>
    /// The site settings group of the Shipping settings.
    /// </summary>
    public const string SettingsGroupId = "shipping";

    /// <summary>
    /// The name of the recipe and deployment step that sets shipping zones and their methods.
    /// </summary>
    public const string ZonesRecipeStep = "ShippingZones";

    /// <summary>
    /// The key of the checkout step that asks where and how an order ships.
    /// </summary>
    public const string CheckoutStepKey = "Shipping";

    /// <summary>
    /// The YesSql collection shipments and their index live in.
    /// </summary>
    public const string CollectionName = "Shipping";

    public static class Features
    {
        public const string EasyPost = "CrestApps.OrchardCore.Shipping.EasyPost";

        public const string Shipping = "CrestApps.OrchardCore.Shipping";
    }

    public static class Parts
    {
        public const string ShippingPart = "ShippingPart";
    }

    /// <summary>
    /// The rate providers that come with Shipping.
    /// </summary>
    public static class Providers
    {
        /// <summary>
        /// One amount per order, plus an optional amount per item.
        /// </summary>
        public const string Flat = "Flat";

        /// <summary>
        /// An amount looked up by the order's weight, subtotal or number of items.
        /// </summary>
        public const string Table = "Table";

        /// <summary>
        /// Nothing to pay, optionally only above an order subtotal.
        /// </summary>
        public const string Free = "Free";

        public const string LocalDelivery = "LocalDelivery";

        /// <summary>
        /// Rates the carriers quote for each order, through a shipping account such as EasyPost.
        /// </summary>
        public const string Carrier = "Carrier";

        /// <summary>
        /// The provider name an order picked up from one of the store's locations carries.
        /// </summary>
        public const string Pickup = Orders.Core.Models.OrderShipping.PickupProviderName;
    }
}
