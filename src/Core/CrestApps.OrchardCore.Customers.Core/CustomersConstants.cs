namespace CrestApps.OrchardCore.Customers.Core;

public static class CustomersConstants
{
    /// <summary>
    /// The YesSql collection customer records and their index live in.
    /// </summary>
    public const string CollectionName = "Customers";

    /// <summary>
    /// The key of the checkout step that asks who an order is for and where it is billed.
    /// </summary>
    public const string ContactStepKey = "Contact";

    /// <summary>
    /// The content type whose fields the checkout asks for on its Contact step, when the Checkout details feature is on.
    /// The store adds the fields it needs with the content type editor.
    /// </summary>
    public const string CheckoutDetailsContentType = "CheckoutDetails";

    /// <summary>
    /// The site settings group of the Customers settings.
    /// </summary>
    public const string SettingsGroupId = "customers";

    public const string LoyaltySettingsGroupId = "loyalty";

    public const string ReferralSettingsGroupId = "referrals";

    public static class Recipes
    {
        /// <summary>
        /// The recipe step that adds or updates guest and business customers.
        /// </summary>
        public const string Customers = "Customers";
    }

    public static class Features
    {
        public const string Customers = "CrestApps.OrchardCore.Customers";

        public const string CheckoutDetails = "CrestApps.OrchardCore.Customers.CheckoutDetails";

        public const string StoreCredit = "CrestApps.OrchardCore.Customers.StoreCredit";

        public const string Loyalty = "CrestApps.OrchardCore.Customers.Loyalty";

        public const string Referrals = "CrestApps.OrchardCore.Customers.Referrals";
    }
}
