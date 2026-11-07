namespace CrestApps.OrchardCore.Webhooks.Core;

/// <summary>
/// How long delivered and failed webhook deliveries are kept in the delivery log, and which events' deliveries are never
/// purged.
/// </summary>
/// <remarks>
/// A feature whose delivery log must be kept, such as AI Webhooks (its log was never deleted before AI events moved to
/// the Webhooks building block), adds its event names to <see cref="KeptEventNames"/>:
/// <code>
/// services.Configure&lt;WebhookDeliveryRetentionOptions&gt;(options => options.KeptEventNames.Add("my.event"));
/// </code>
/// </remarks>
public sealed class WebhookDeliveryRetentionOptions
{
    /// <summary>
    /// Gets or sets how long delivered and failed deliveries are kept; 30 days by default. Zero or less keeps them all.
    /// Pending deliveries are never purged.
    /// </summary>
    public TimeSpan DeliveryRetention { get; set; } = WebhookStorage.DeliveryRetention;

    /// <summary>
    /// Gets the names of the events whose deliveries are never purged, compared ordinally.
    /// </summary>
    public ISet<string> KeptEventNames { get; } = new HashSet<string>(StringComparer.Ordinal);
}
