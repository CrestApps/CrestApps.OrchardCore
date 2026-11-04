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
        /// The durable provider webhook inbox. The ID keeps the value it was first released with, so tenants that
        /// enabled it keep it.
        /// </summary>
        public const string ProviderInbox = "CrestApps.OrchardCore.ContactCenter.ProviderInbox";
    }
}
