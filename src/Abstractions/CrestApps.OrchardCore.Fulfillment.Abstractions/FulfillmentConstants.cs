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
    /// The notification type raised when a fulfillment request needs a person.
    /// </summary>
    public const string NeedsAttentionNotificationType = "FulfillmentNeedsAttention";

    /// <summary>
    /// How long the engine waits for a partner to answer <see cref="Services.IFulfillmentProvider.SubmitAsync"/> before it
    /// treats the outcome as unclear.
    /// </summary>
    public static readonly TimeSpan SubmitTimeout = TimeSpan.FromMinutes(2);

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
