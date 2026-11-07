namespace CrestApps.OrchardCore.Webhooks.Core;

/// <summary>
/// Where outbound webhook subscriptions and deliveries are stored, and the names their data depends on.
/// </summary>
public static class WebhookStorage
{
    /// <summary>
    /// The YesSql collection subscriptions and deliveries live in.
    /// </summary>
    public const string CollectionName = "Webhooks";

    /// <summary>
    /// The data protection purpose subscription secrets are protected with. It keeps the value Commerce Webhooks
    /// released, or every stored secret becomes unreadable.
    /// </summary>
    public const string SecretProtectionPurpose = "CrestApps.OrchardCore.Commerce.Webhooks.Secret";

    /// <summary>
    /// The named, egress-guarded HTTP client webhooks are posted with.
    /// </summary>
    public const string HttpClientName = "CrestApps.Webhooks";

    /// <summary>
    /// How long delivered and failed deliveries are kept in the log.
    /// </summary>
    public static readonly TimeSpan DeliveryRetention = TimeSpan.FromDays(30);
}
