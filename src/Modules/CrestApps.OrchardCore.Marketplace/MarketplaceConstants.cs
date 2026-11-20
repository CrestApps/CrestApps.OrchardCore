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
    /// The party kind of a vendor, as the payee of a disbursement.
    /// </summary>
    public const string VendorPartyKind = "MarketplaceVendor";

    /// <summary>
    /// The source kind of a vendor payout's disbursement: the order whose earnings it pays.
    /// </summary>
    public const string PayoutSourceKind = "MarketplaceOrder";

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
