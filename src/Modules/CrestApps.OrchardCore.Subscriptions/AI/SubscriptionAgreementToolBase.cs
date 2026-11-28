using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Subscriptions.AI;

/// <summary>
/// A subscription agreement tool: it needs <see cref="SubscriptionPermissions.ManageSubscriptions"/>, the permission of
/// Subscriptions › Agreements, and shares finding an agreement and describing it from an explicit list of fields.
/// </summary>
internal abstract class SubscriptionAgreementToolBase : SubscriptionsAIToolBase
{
    /// <inheritdoc/>
    protected override Permission Permission => SubscriptionPermissions.ManageSubscriptions;

    /// <summary>
    /// Finds an agreement by its id.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="id">The agreement's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The agreement, or the sentence that says it was not found.</returns>
    protected static async Task<(Subscription Subscription, string Error)> FindAsync(IServiceProvider services, string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id))
        {
            return (null, "Nothing was done: give the agreement's subscriptionId. Use listSubscriptions to find it.");
        }

        var subscription = await services.GetRequiredService<ISubscriptionManager>().FindByIdAsync(id, cancellationToken);

        return subscription is null
            ? (null, $"Nothing was done: no subscription agreement has the id '{id}'. Use listSubscriptions to find it.")
            : (subscription, null);
    }

    /// <summary>
    /// Describes an agreement for a list row or the head of its details.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="subscription">The agreement.</param>
    /// <returns>The allow-listed fields.</returns>
    protected static async Task<Dictionary<string, object>> SummarizeAsync(IServiceProvider services, Subscription subscription)
    {
        var ownerName = await SubscriptionOwnerNames.ResolveAsync(services, subscription.OwnerId);

        return new Dictionary<string, object>
        {
            ["id"] = subscription.ItemId,
            ["title"] = subscription.Title,
            ["status"] = ToCamel(subscription.Status.ToString()),
            ["owner"] = ownerName ?? subscription.GuestContactName,
            ["isGuest"] = string.IsNullOrEmpty(subscription.OwnerId),
            ["amount"] = subscription.Amount,
            ["tax"] = subscription.TaxAmount,
            ["total"] = subscription.TotalAmount,
            ["currency"] = subscription.Currency,
            ["billingEvery"] = $"{subscription.BillingDuration} {subscription.DurationType}",
            ["cyclesBilled"] = subscription.CyclesBilled,
            ["cycleLimit"] = subscription.BillingCycleLimit,
            ["currentPeriodEnds"] = await LocalTimeAsync(services, subscription.CurrentPeriodEndUtc),
            ["nextBilling"] = await LocalTimeAsync(services, subscription.NextBillingUtc),
            ["trialEnds"] = await LocalTimeAsync(services, subscription.TrialEndsUtc),
            ["pastDueSince"] = await LocalTimeAsync(services, subscription.PastDueSinceUtc),
            ["cancelAtPeriodEnd"] = subscription.CancelAtPeriodEnd,
            ["canceled"] = await LocalTimeAsync(services, subscription.CanceledUtc),
            ["billedByProvider"] = string.IsNullOrEmpty(subscription.ProviderSubscriptionId) ? null : subscription.ProviderKey,
            ["created"] = await LocalTimeAsync(services, subscription.CreatedUtc),
        };
    }

    /// <summary>
    /// Gets the admin path of an agreement's page.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="subscription">The agreement.</param>
    /// <returns>The path, or <see langword="null"/>.</returns>
    protected static string GetAgreementPath(IServiceProvider services, Subscription subscription)
        => GetAdminPath(services, "SubscriptionAgreementsDetail", new { area = SubscriptionConstants.Features.Area, itemId = subscription.ItemId });

    /// <summary>
    /// Says what happens at the payment provider after a change, since the provider is told after the agreement is
    /// saved and a refusal there is recorded as a payment issue rather than returned.
    /// </summary>
    /// <param name="subscription">The agreement after the change.</param>
    /// <returns>The sentence, or <see langword="null"/> when the store bills the agreement itself.</returns>
    protected static string DescribeProvider(Subscription subscription)
        => string.IsNullOrEmpty(subscription.ProviderSubscriptionId)
            ? null
            : $"The change was sent to the payment provider '{subscription.ProviderKey}'. If the provider refused it, a payment issue was recorded and the customer may still be billed: check Payment issues and the provider's dashboard.";
}
