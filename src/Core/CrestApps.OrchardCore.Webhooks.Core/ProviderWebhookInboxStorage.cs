namespace CrestApps.OrchardCore.Webhooks.Core;

/// <summary>
/// Where the provider webhook inbox is stored.
/// </summary>
public static class ProviderWebhookInboxStorage
{
    /// <summary>
    /// The YesSql collection inbox documents and their index live in. The inbox was first released inside Contact
    /// Center and its rows already live in that collection, so the name stays.
    /// </summary>
    public const string CollectionName = "ContactCenter";
}
