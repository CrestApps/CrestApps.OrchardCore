namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Resolves a provider name or alias to the one canonical name a provider webhook delivery is keyed by, so the aliases
/// of one provider share one idempotency key. The default implementation returns the name unchanged; a module that
/// knows provider aliases (Telephony does) registers its own.
/// </summary>
public interface IWebhookProviderNameNormalizer
{
    /// <summary>
    /// Returns the canonical name of a provider.
    /// </summary>
    /// <param name="providerName">The provider name or alias.</param>
    /// <returns>The canonical name, or the name unchanged when it is not a known alias.</returns>
    string Normalize(string providerName);
}
