namespace CrestApps.OrchardCore.Fulfillment;

/// <summary>
/// The constants of the Fulfillment Partners module.
/// </summary>
public static class FulfillmentConstants
{
    /// <summary>
    /// The site settings group of the fulfillment partners; each provider module adds its connection section to it.
    /// </summary>
    public const string SettingsGroupId = "fulfillment-partners";

    /// <summary>
    /// The YesSql collection fulfillment records are stored in.
    /// </summary>
    public const string CollectionName = "Fulfillment";

    /// <summary>
    /// The feature identifiers.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Fulfillment partners: the settings page that outside companies which make and ship products connect on.
        /// </summary>
        public const string Fulfillment = "CrestApps.OrchardCore.Fulfillment";
    }
}
