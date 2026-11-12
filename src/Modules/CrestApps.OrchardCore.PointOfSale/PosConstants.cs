namespace CrestApps.OrchardCore.PointOfSale;

public static class PosConstants
{
    /// <summary>
    /// The request group of Orchard Core's Rate Limits the till's payment and return routes are tagged with: 60 a minute
    /// per user.
    /// </summary>
    public const string PaymentRateLimitGroup = "pos-payment";

    public const string Feature = "CrestApps.OrchardCore.PointOfSale";

    /// <summary>
    /// The YesSql collection registers and shifts live in.
    /// </summary>
    public const string CollectionName = "PointOfSale";

    /// <summary>
    /// The tender a sale's cash is taken by. Its reference is the cash handed over, in the currency's minor units.
    /// </summary>
    public const string CashTenderKey = "cash";

    /// <summary>
    /// The payment method for a card taken on the store's own card terminal, outside the site.
    /// </summary>
    public const string CounterCardProviderKey = "pos-card";

    /// <summary>
    /// The payment method for a card presented on a Stripe Terminal reader, when the Stripe module is on.
    /// </summary>
    public const string TerminalProviderKey = "stripe-terminal";

    /// <summary>
    /// The tag the point-of-sale endpoints are documented under.
    /// </summary>
    public const string ApiTag = "Point of Sale";

    public static class Recipes
    {
        /// <summary>
        /// The recipe step that adds or updates registers.
        /// </summary>
        public const string Registers = "Registers";
    }
}
