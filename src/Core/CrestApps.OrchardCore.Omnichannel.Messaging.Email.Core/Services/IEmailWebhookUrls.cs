namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// Builds the inbound email webhook URLs an operator pastes into their email provider, one per provider format, each
/// carrying the tenant's webhook key.
/// </summary>
public interface IEmailWebhookUrls
{
    /// <summary>
    /// Gets the webhook URL of every registered provider format.
    /// </summary>
    /// <returns>The URLs keyed by provider name; empty while the tenant has no webhook key yet.</returns>
    Task<IReadOnlyList<KeyValuePair<string, string>>> GetAllAsync();

    /// <summary>
    /// Gets the delivery events webhook URL (bounces, complaints, blocks) of every registered provider format.
    /// </summary>
    /// <returns>The URLs keyed by provider name; empty while the tenant has no webhook key yet.</returns>
    Task<IReadOnlyList<KeyValuePair<string, string>>> GetDeliveryEventUrlsAsync();
}
