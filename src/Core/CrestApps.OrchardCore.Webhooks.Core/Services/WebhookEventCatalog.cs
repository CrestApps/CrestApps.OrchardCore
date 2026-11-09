using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Gathers the events every registered <see cref="IWebhookEventProvider"/> declares. The first declaration of a name
/// wins.
/// </summary>
public sealed class WebhookEventCatalog : IWebhookEventCatalog
{
    private readonly IEnumerable<IWebhookEventProvider> _providers;

    private IReadOnlyList<WebhookEventDescriptor> _events;
    private Dictionary<string, WebhookEventDescriptor> _byName;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookEventCatalog"/> class.
    /// </summary>
    /// <param name="providers">The event providers of the enabled features.</param>
    public WebhookEventCatalog(IEnumerable<IWebhookEventProvider> providers)
    {
        _providers = providers;
    }

    /// <inheritdoc/>
    public IReadOnlyList<WebhookEventDescriptor> GetEvents()
    {
        EnsureLoaded();

        return _events;
    }

    /// <inheritdoc/>
    public WebhookEventDescriptor Find(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        EnsureLoaded();

        return _byName.GetValueOrDefault(name);
    }

    private void EnsureLoaded()
    {
        if (_events is not null)
        {
            return;
        }

        var byName = new Dictionary<string, WebhookEventDescriptor>(StringComparer.Ordinal);

        foreach (var provider in _providers)
        {
            foreach (var descriptor in provider.GetEvents() ?? [])
            {
                if (descriptor is not null)
                {
                    byName.TryAdd(descriptor.Name, descriptor);
                }
            }
        }

        _byName = byName;
        _events = [.. byName.Values
            .OrderBy(descriptor => descriptor.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(descriptor => descriptor.Name, StringComparer.Ordinal)];
    }
}
