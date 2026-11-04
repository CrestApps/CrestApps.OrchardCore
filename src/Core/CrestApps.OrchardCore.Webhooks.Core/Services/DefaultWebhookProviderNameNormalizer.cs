namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Keys provider webhook deliveries by the provider name exactly as the endpoint gave it. A module that knows provider
/// aliases replaces it.
/// </summary>
public sealed class DefaultWebhookProviderNameNormalizer : IWebhookProviderNameNormalizer
{
    /// <inheritdoc/>
    public string Normalize(string providerName) => providerName;
}
