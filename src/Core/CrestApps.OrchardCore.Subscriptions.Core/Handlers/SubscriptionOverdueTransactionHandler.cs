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

        // A renewal bills the subscription itself; the first payment belongs to the checkout that created it.
        if (string.Equals(transaction.ReferenceType, SubscriptionReferenceType, StringComparison.Ordinal) && !string.IsNullOrEmpty(transaction.ReferenceId))
        {
            await _lifecycleService.MarkPastDueAsync(transaction.ReferenceId, reason, cancellationToken);

            return;
        }

        if (string.IsNullOrEmpty(transaction.CheckoutSessionId))
        {
            return;
        }

        foreach (var subscription in await _subscriptionManager.GetByCheckoutSessionAsync(transaction.CheckoutSessionId, cancellationToken))
        {
            if (subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing)
            {
                await _lifecycleService.MarkPastDueAsync(subscription.ItemId, reason, cancellationToken);
            }
        }
    }
}
