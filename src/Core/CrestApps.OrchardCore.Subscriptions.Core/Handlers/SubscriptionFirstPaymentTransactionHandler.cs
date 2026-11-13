using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Starts a subscription that was bought with a promise to pay (Pay Later, a bank transfer) once its first transaction
/// is paid. Until then it waits as <see cref="SubscriptionStatus.Incomplete"/> and grants nothing.
/// </summary>
public sealed class SubscriptionFirstPaymentTransactionHandler : TransactionEventHandlerBase
{
    private readonly ISubscriptionLifecycleService _lifecycleService;
    private readonly ISubscriptionManager _subscriptionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFirstPaymentTransactionHandler"/> class.
    /// </summary>
    public SubscriptionFirstPaymentTransactionHandler(ISubscriptionLifecycleService lifecycleService, ISubscriptionManager subscriptionManager)
    {
        _lifecycleService = lifecycleService;
        _subscriptionManager = subscriptionManager;
    }

    /// <inheritdoc/>
    public override async Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
    {
        if (current != TransactionStatus.Paid || string.IsNullOrEmpty(transaction?.CheckoutSessionId))
        {
            return;
        }

        foreach (var subscription in await _subscriptionManager.GetByCheckoutSessionAsync(transaction.CheckoutSessionId, cancellationToken))
        {
            // Pay Later records one transaction per obligation of the checkout, so a plan with a sign-up fee, or a cart with a
            // one-time item next to the plan, leaves more than one. Only the transaction for the subscription's own obligation
            // pays for its first cycle; paying the one-time balance, or a later cycle, must not start the plan.
            if (subscription.Status == SubscriptionStatus.Incomplete && PaysFor(transaction, subscription))
            {
                await _lifecycleService.ResumeAsync(subscription.ItemId, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="transaction"/> is the first payment of <paramref name="subscription"/>: the transaction
    /// recorded for the obligation the subscription settles.
    /// </summary>
    /// <param name="transaction">The paid transaction.</param>
    /// <param name="subscription">A subscription of the same checkout.</param>
    internal static bool PaysFor(Transaction transaction, Subscription subscription)
        => !string.IsNullOrEmpty(subscription?.ObligationId) &&
            string.Equals(transaction?.ObligationId, subscription.ObligationId, StringComparison.Ordinal);
}
