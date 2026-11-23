using CrestApps.OrchardCore.Plaid.Models;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Acts on Plaid webhooks. The Plaid webhook endpoint checks a webhook's signature once, stores it in the provider
/// webhook inbox, and hands every webhook, whatever its type and code (such as <c>TRANSACTIONS: SYNC_UPDATES_AVAILABLE</c>
/// or <c>ITEM: ERROR</c>), to every registered handler; a handler picks the ones it cares about, usually by
/// <see cref="PlaidWebhook.WebhookType"/>, <see cref="PlaidWebhook.WebhookCode"/> and <see cref="PlaidWebhook.ItemId"/>,
/// and returns at once for the rest. Register one with
/// <see cref="PlaidServiceCollectionExtensions.AddPlaidWebhookHandler{THandler}"/>.
/// </summary>
/// <remarks>
/// Delivery is at least once: Plaid resends a webhook it got no answer for, and the inbox dispatches a stored webhook
/// again when a handler throws. A handler must therefore be idempotent. When a handler throws, the other handlers
/// still run and the webhook is retried afterwards.
/// </remarks>
public interface IPlaidWebhookHandler
{
    /// <summary>
    /// Acts on a verified webhook.
    /// </summary>
    /// <param name="context">The webhook and its exact body.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task HandleAsync(PlaidWebhookContext context, CancellationToken cancellationToken = default);
}
