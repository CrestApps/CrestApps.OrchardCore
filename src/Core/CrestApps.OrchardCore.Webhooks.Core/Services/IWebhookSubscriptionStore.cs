using CrestApps.Core.Services;
using CrestApps.OrchardCore.Webhooks.Core.Models;

namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Stores webhook subscriptions.
/// </summary>
public interface IWebhookSubscriptionStore : ICatalog<WebhookSubscription>
{
    /// <summary>
    /// Lists the subscriptions that send events.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The enabled subscriptions.</returns>
    Task<IReadOnlyList<WebhookSubscription>> ListEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists a page of subscriptions, newest first.
    /// </summary>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many there are, and the page.</returns>
    Task<(int Count, IReadOnlyList<WebhookSubscription> Subscriptions)> PageAsync(int skip, int take, CancellationToken cancellationToken = default);
}
