using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Recipes;

namespace CrestApps.OrchardCore.Webhooks.Deployments;

/// <summary>
/// Writes the webhook subscriptions into a <c>WebhookSubscriptions</c> recipe step: name, URL, events and whether each
/// sends, keyed by its id. The protected secret and the stamps the storing site owns are left out.
/// </summary>
internal sealed class WebhookSubscriptionsDeploymentSource : CatalogDeploymentSourceBase<WebhookSubscriptionsDeploymentStep, WebhookSubscription>
{
    private readonly IWebhookSubscriptionStore _subscriptions;

    public WebhookSubscriptionsDeploymentSource(IWebhookSubscriptionStore subscriptions)
        : base(WebhookSubscriptionsStep.EntriesName)
    {
        _subscriptions = subscriptions;
    }

    protected override async Task<IEnumerable<WebhookSubscription>> GetEntriesAsync()
        => (await _subscriptions.GetAllAsync()).OrderBy(subscription => subscription.Name, StringComparer.OrdinalIgnoreCase);

    protected override JsonObject Export(WebhookSubscription entry)
        => WebhookSubscriptionsStep.Export(entry);
}
