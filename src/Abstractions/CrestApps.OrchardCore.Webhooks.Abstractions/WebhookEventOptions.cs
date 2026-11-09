using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// The events declared with <see cref="WebhookEventServiceCollectionExtensions.AddWebhookEvent"/>.
/// </summary>
public sealed class WebhookEventOptions
{
    /// <summary>
    /// Gets the declared events.
    /// </summary>
    public IList<WebhookEventDescriptor> Events { get; } = [];
}
