namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Feature IDs and shared names of the webhooks building block.
/// </summary>
public static class WebhooksConstants
{
    /// <summary>
    /// The feature IDs.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// Outbound webhooks: subscriptions, the delivery outbox and their administration.
        /// </summary>
        public const string Webhooks = "CrestApps.OrchardCore.Webhooks";

        /// <summary>
        /// The durable provider webhook inbox. The ID keeps the value it was first released with, so tenants that
        /// enabled it keep it.
        /// </summary>
        public const string ProviderInbox = "CrestApps.OrchardCore.ContactCenter.ProviderInbox";
    }
}
