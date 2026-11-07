using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Every event the tenant's enabled modules publish to webhooks, gathered from the registered
/// <see cref="IWebhookEventProvider"/> implementations.
/// </summary>
public interface IWebhookEventCatalog
{
    /// <summary>
    /// Returns every declared event, once per name, ordered by group and name.
    /// </summary>
    /// <returns>The events.</returns>
    IReadOnlyList<WebhookEventDescriptor> GetEvents();

    /// <summary>
    /// Returns the declared event of a name, or <see langword="null"/> when no enabled module declares it.
    /// </summary>
    /// <param name="name">The event's name.</param>
    /// <returns>The event, or <see langword="null"/>.</returns>
    WebhookEventDescriptor Find(string name);
}
