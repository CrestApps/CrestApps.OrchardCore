namespace CrestApps.OrchardCore.Commerce.Webhooks;

public static class WebhookConstants
{
    public const string Feature = CommerceConstants.Features.Webhooks;

    /// <summary>
    /// The YesSql collection webhooks and their deliveries live in.
    /// </summary>
    public const string CollectionName = "Webhooks";

    /// <summary>
    /// The event name a subscription lists to take every event.
    /// </summary>
    public const string AllEvents = "*";

    /// <summary>
    /// The named HTTP client deliveries are posted with.
    /// </summary>
    public const string HttpClientName = "CommerceWebhooks";

    /// <summary>
    /// The purpose subscription secrets are protected with.
    /// </summary>
    public const string SecretProtectionPurpose = "CrestApps.OrchardCore.Commerce.Webhooks.Secret";

    /// <summary>
    /// The headers every delivery carries.
    /// </summary>
    public static class Headers
    {
        /// <summary>
        /// The event's id, the same on every attempt and for every subscription: a receiver drops one it has seen.
        /// </summary>
        public const string Id = "X-Webhook-Id";

        public const string Event = "X-Webhook-Event";

        /// <summary>
        /// When it was signed, in seconds since 1970. A receiver refuses one signed long ago.
        /// </summary>
        public const string Timestamp = "X-Webhook-Timestamp";

        /// <summary>
        /// <c>v1=</c> and the lowercase hex HMAC-SHA256 of <c>{timestamp}.{body}</c> keyed with the secret.
        /// </summary>
        public const string Signature = "X-Webhook-Signature";
    }
}
