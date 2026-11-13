using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Keeps an offline agreement's invoices in step with its subscription: a subscription that waits for its first payment
/// or is paused holds the next cycle, and one that ended stops the schedule.
/// </summary>
public sealed class SubscriptionTransactionRenewalFilter : ITransactionRenewalFilter
{
    private const string CycleSeparator = ":cycle-";

    private readonly ISubscriptionManager _subscriptionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionTransactionRenewalFilter"/> class.
    /// </summary>
    /// <param name="subscriptionManager">The subscription manager.</param>
    public SubscriptionTransactionRenewalFilter(ISubscriptionManager subscriptionManager)
        => _subscriptionManager = subscriptionManager;

    /// <inheritdoc/>
    public async Task<TransactionRenewalDecision> DecideAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(transaction?.CheckoutSessionId) || string.IsNullOrEmpty(transaction.ObligationId))
        {
            return TransactionRenewalDecision.Renew;
        }

        var subscriptions = await _subscriptionManager.GetByCheckoutSessionAsync(transaction.CheckoutSessionId, cancellationToken);
        var subscription = subscriptions.FirstOrDefault(candidate => Bills(candidate, transaction));

        if (subscription is null)
        {
            // Not an agreement this module keeps, so nothing here to say about it.
            return TransactionRenewalDecision.Renew;
        }

        if (subscription.CancelAtPeriodEnd)
        {
            return TransactionRenewalDecision.Stop;
        }

        return subscription.Status switch
        {
            // Falling behind on one invoice does not end the agreement; the ledger shows the growing debt and the overdue
            // sweep moves it to past due.
            SubscriptionStatus.Active or SubscriptionStatus.Trialing or SubscriptionStatus.PastDue => TransactionRenewalDecision.Renew,

            // Not started yet, or suspended: nothing more is owed until it is current again.
            SubscriptionStatus.Incomplete or SubscriptionStatus.Paused => TransactionRenewalDecision.Hold,

            _ => TransactionRenewalDecision.Stop,
        };
    }

    // The first cycle carries the subscription's own obligation; each renewal appends ":cycle-<n>" to the one before it.
    private static bool Bills(Subscription subscription, Transaction transaction)
        => !string.IsNullOrEmpty(subscription.ObligationId) &&
            (string.Equals(transaction.ObligationId, subscription.ObligationId, StringComparison.Ordinal) ||
                transaction.ObligationId.StartsWith(subscription.ObligationId + CycleSeparator, StringComparison.Ordinal));
}
