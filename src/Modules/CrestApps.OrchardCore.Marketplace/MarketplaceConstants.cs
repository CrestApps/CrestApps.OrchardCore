namespace CrestApps.OrchardCore.Marketplace;

public static class MarketplaceConstants
{
    public const string Feature = "CrestApps.OrchardCore.Marketplace";

    /// <summary>
    /// The feature that pays vendors out to Stripe Connect accounts.
    /// </summary>
    public const string StripeConnectFeature = "CrestApps.OrchardCore.Marketplace.StripeConnect";

    /// <summary>
    /// The name of the payout provider that pays vendors to Stripe Connect accounts.
    /// </summary>
    public const string StripeConnectPayoutProvider = "StripeConnect";

    /// <summary>
    /// The YesSql collection that holds vendors, their earnings and payouts.
    /// </summary>
    public const string CollectionName = "Marketplace";

    public const string SettingsGroupId = "marketplace";

    public static class Recipes
    {
        /// <summary>
        /// The recipe step that adds or updates vendors.
        /// </summary>
        public const string Vendors = "Vendors";
    }
}
