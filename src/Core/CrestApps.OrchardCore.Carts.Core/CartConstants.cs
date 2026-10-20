namespace CrestApps.OrchardCore.Carts.Core;

/// <summary>
/// Names shared by the Carts module and the modules that build on it.
/// </summary>
public static class CartConstants
{
    /// <summary>
    /// The YesSql collection carts are stored in.
    /// </summary>
    public const string CollectionName = "Carts";

    /// <summary>
    /// The rate limit group of the cart endpoints, for the optional Orchard Core Rate Limits module.
    /// </summary>
    public const string RateLimitGroup = "Cart";

    public static class Features
    {
        public const string Carts = "CrestApps.OrchardCore.Carts";

        /// <summary>
        /// The cart API under <c>api/commerce/v1</c>.
        /// </summary>
        public const string Wishlists = "CrestApps.OrchardCore.Carts.Wishlists";

        public const string Api = "CrestApps.OrchardCore.Carts.Api";
    }

    public static class RouteNames
    {
        public const string Cart = "Cart";

        public const string Add = "CartAdd";

        /// <summary>
        /// Adds several products at once, such as the ones a grouped product shows.
        /// </summary>
        public const string AddMany = "CartAddMany";

        public const string Update = "CartUpdate";

        public const string Remove = "CartRemove";

        /// <summary>
        /// The route that turns the cart into a purchase. The Orders module registers it; the cart page offers
        /// a checkout button only when it exists.
        /// </summary>
        public const string Checkout = "CartCheckout";
    }
}
