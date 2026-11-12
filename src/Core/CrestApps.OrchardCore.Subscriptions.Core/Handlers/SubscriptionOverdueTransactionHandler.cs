using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Moves a subscription to past due when its transaction becomes overdue. Without it a subscription bought with a
/// promise to pay (Pay Later, a bank transfer) stayed active however long the payment was owed.
/// </summary>
public sealed class SubscriptionOverdueTransactionHandler : TransactionEventHandlerBase
{
    private const string SubscriptionReferenceType = "Subscription";

    private readonly ISubscriptionLifecycleService _lifecycleService;
    private readonly ISubscriptionManager _subscriptionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionOverdueTransactionHandler"/> class.
    /// </summary>
    public SubscriptionOverdueTransactionHandler(ISubscriptionLifecycleService lifecycleService, ISubscriptionManager subscriptionManager)
    {
        _lifecycleService = lifecycleService;
        _subscriptionManager = subscriptionManager;
    }

    /// <inheritdoc/>
    public override async Task BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        const string reason = "A payment for this subscription is overdue.";

        // The checkout that created a subscription is what every transaction of it carries: the first payment, and a Pay
        // Later renewal copied from it. Its reference is not enough: a plan sign-up names the plan there (Subscription,
        // the plan's content item id), not the subscription.
        if (!string.IsNullOrEmpty(transaction.CheckoutSessionId))
        {
            var subscriptions = await _subscriptionManager.GetByCheckoutSessionAsync(transaction.CheckoutSessionId, cancellationToken);

            if (subscriptions.Count > 0)
            {
                foreach (var subscription in subscriptions)
                {
                    if (subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing)
                    {
                        await _lifecycleService.MarkPastDueAsync(subscription.ItemId, reason, cancellationToken);
                    }
                }

                return;
            }
        }

        // A renewal billed to the subscription itself names it.
        if (string.Equals(transaction.ReferenceType, SubscriptionReferenceType, StringComparison.Ordinal) && !string.IsNullOrEmpty(transaction.ReferenceId))
        {
            await _lifecycleService.MarkPastDueAsync(transaction.ReferenceId, reason, cancellationToken);
        }
    }
}
