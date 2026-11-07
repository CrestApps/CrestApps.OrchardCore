namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// The headers every outbound webhook delivery carries.
/// </summary>
public static class WebhookHeaders
{
    /// <summary>
    /// The event's id, the same on every attempt and for every subscription: a receiver drops one it has seen.
    /// </summary>
    public const string Id = "X-Webhook-Id";

    /// <summary>
    /// The event's name.
    /// </summary>
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
