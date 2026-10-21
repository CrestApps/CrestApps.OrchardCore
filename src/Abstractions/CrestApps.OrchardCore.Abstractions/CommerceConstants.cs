namespace CrestApps.OrchardCore.Commerce;

/// <summary>
/// Provides shared constant values for the Commerce module that owns the common Commerce admin menu.
/// </summary>
public static class CommerceConstants
{
    /// <summary>
    /// Contains the feature identifiers exposed by the Commerce module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Commerce feature that registers the shared Commerce admin menu and its icon. It is enabled
        /// by dependency only, so any module that contributes to the Commerce menu should depend on it.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Commerce";

        /// <summary>
        /// The Commerce Webhooks feature. Modules register what tells webhooks about their records behind
        /// <c>[RequireFeatures]</c> on it.
        /// </summary>
        public const string Webhooks = "CrestApps.OrchardCore.Commerce.Webhooks";
    }

    /// <summary>
    /// The storefront account: the pages a signed-in customer opens for their own orders, downloads, addresses and
    /// more. Each module adds its pages to the account menu with a <c>NamedNavigationProvider</c> created for
    /// <see cref="MenuName"/>.
    /// </summary>
    public static class Account
    {
        /// <summary>
        /// The name of the navigation menu that lists the account's pages.
        /// </summary>
        public const string MenuName = "commerce-account";

        /// <summary>
        /// The route name of the account's home page, at <c>/account</c>.
        /// </summary>
        public const string HomeRouteName = "CommerceAccount";

        /// <summary>
        /// The shape that renders the account menu beside an account page.
        /// </summary>
        public const string NavigationShape = "CommerceAccountNavigation";
    }
}
