namespace CrestApps.OrchardCore.Commerce;

/// <summary>
/// Provides shared constant values for the Commerce module: the Commerce Foundation feature that owns the common
/// Commerce admin menu and customer account, and the Commerce feature, the store layer on top of it.
/// </summary>
public static class CommerceConstants
{
    /// <summary>
    /// Contains the feature identifiers exposed by the Commerce module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Commerce feature: the store layer, with the store's setup guide (Commerce › Set up your store) and the
        /// roles it adds for a store's staff. It is enabled by dependency only and brings <see cref="Foundation"/>; the
        /// store modules (products, carts, checkout, orders, shipping and the like) depend on it.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Commerce";

        /// <summary>
        /// The Commerce Foundation feature: what several features share without turning on the store. It registers the
        /// top-level Commerce admin menu and its icon, the customer account (<c>/account</c> and its menu), the helpers
        /// of the commerce admin lists, customers' access to the admin pages of their own data, and the export and
        /// anonymization of a person's commerce data. It is enabled by dependency only; a building block that adds to
        /// the Commerce menu or the account without being part of a store, such as transactions, exchange
        /// rates or customers, depends on it rather than on <see cref="Area"/>.
        /// </summary>
        public const string Foundation = "CrestApps.OrchardCore.Commerce.Foundation";

        /// <summary>
        /// The Commerce Webhooks feature. Modules register what tells webhooks about their records behind
        /// <c>[RequireFeatures]</c> on it.
        /// </summary>
        public const string Webhooks = "CrestApps.OrchardCore.Commerce.Webhooks";

        /// <summary>
        /// The Store Profile feature: the store's name, logo, tax ID, contact and address under Settings › Commerce ›
        /// Store, which receipts, the ship-from address and the tax origin fall back to.
        /// </summary>
        public const string StoreProfile = "CrestApps.OrchardCore.Commerce.StoreProfile";
    }

    /// <summary>
    /// The storefront account: the pages a signed-in customer opens for their own orders, downloads, addresses and
    /// more. Each module adds its pages to the account menu with a <c>NamedNavigationProvider</c> created for
    /// <see cref="MenuName"/>.
    /// </summary>
    /// <remarks>
    /// The account area moved to the Users module (<c>UsersConstants.Account</c>); these keep the same values so code
    /// that uses them keeps working. A test pins the two together.
    /// </remarks>
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

        /// <summary>
        /// The shape that frames an account page: the account menu beside the page's heading and content. A page renders
        /// it with its <c>Title</c>, its <c>Content</c> (a shape or HTML) and, optionally, <c>Actions</c> beside the
        /// heading, and keeps its own breadcrumb. A theme overrides it to change every account page.
        /// </summary>
        public const string PageShape = "CommerceAccountPage";
    }

    /// <summary>
    /// The shapes the Commerce module renders for the other commerce modules, which a theme can override.
    /// </summary>
    public static class Shapes
    {
        /// <summary>
        /// The filters at the end of a commerce admin list's toolbar. Its model is an <c>AdminListFiltersViewModel</c>,
        /// and it has the alternate <c>CommerceAdminListFilters__{ListName}</c>.
        /// </summary>
        public const string AdminListFilters = "CommerceAdminListFilters";
    }

    /// <summary>
    /// The names of the roles the store setup guide adds for a store's staff. Each commerce module grants its own
    /// permissions to these roles through <c>IPermissionProvider.GetDefaultStereotypes()</c>, so a role gets them when
    /// it is created and when a module that grants it more is enabled later.
    /// </summary>
    public static class StaffRoles
    {
        /// <summary>
        /// Runs the store day to day: products, orders, stock, shipping, customers, promotions and reports.
        /// </summary>
        public const string StoreManager = "Store manager";

        /// <summary>
        /// Picks, packs and ships orders, and receives and counts stock.
        /// </summary>
        public const string Warehouse = "Warehouse";

        /// <summary>
        /// Answers customers: looks up their orders, changes and refunds them, and manages subscriptions and bookings.
        /// </summary>
        public const string CustomerSupport = "Customer support";

        /// <summary>
        /// Runs promotions, campaigns, reviews and product feeds, and edits products and pages.
        /// </summary>
        public const string Marketing = "Marketing";
    }
}
