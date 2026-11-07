using CrestApps.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.Webhooks.Core.Models;

/// <summary>
/// A URL the site posts events to: which events, and the secret it signs them with.
/// </summary>
/// <remarks>
/// YesSql finds a document by its stored type name. Subscriptions were first stored by Commerce Webhooks, so the record
/// keeps that name and every subscription a tenant already has stays readable.
/// </remarks>
[SimplifiedTypeName(StoredTypeName)]
public sealed class WebhookSubscription : CatalogItem
{
    /// <summary>
    /// The type name subscriptions are stored under. It never changes.
    /// </summary>
    public const string StoredTypeName = "CrestApps.OrchardCore.Commerce.Webhooks.Models.WebhookSubscription, CrestApps.OrchardCore.Commerce.Webhooks";

    /// <summary>
    /// Gets or sets the YesSql document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets what the administrator calls it, such as the system it feeds.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the URL events are posted to.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the secret payloads are signed with, protected at rest.
    /// </summary>
    public string ProtectedSecret { get; set; }

    /// <summary>
    /// Gets or sets the events sent, by name; <see cref="WebhookEvents.All"/> sends every event.
    /// </summary>
    public List<string> Events { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether events are sent.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets when the subscription was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the subscription was last changed.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Returns whether the subscription takes the event.
    /// </summary>
    /// <param name="eventName">The event's name.</param>
    /// <returns><see langword="true"/> when the subscription is enabled and lists the event or every event.</returns>
    public bool Takes(string eventName)
        => IsEnabled && (Events ?? []).Any(entry => entry == WebhookEvents.All || string.Equals(entry, eventName, StringComparison.Ordinal));
}
