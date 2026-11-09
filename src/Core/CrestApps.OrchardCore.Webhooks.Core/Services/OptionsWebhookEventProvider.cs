using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Offers the events declared with <see cref="WebhookEventServiceCollectionExtensions.AddWebhookEvent"/>.
/// </summary>
public sealed class OptionsWebhookEventProvider : IWebhookEventProvider
{
    private readonly WebhookEventOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OptionsWebhookEventProvider"/> class.
    /// </summary>
    /// <param name="options">The declared events.</param>
    public OptionsWebhookEventProvider(IOptions<WebhookEventOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc/>
    public IEnumerable<WebhookEventDescriptor> GetEvents()
        => _options.Events;
}
