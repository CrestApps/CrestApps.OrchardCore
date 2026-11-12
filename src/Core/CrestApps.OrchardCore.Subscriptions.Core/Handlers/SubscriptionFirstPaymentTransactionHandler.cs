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
            if (subscription.Status == SubscriptionStatus.Incomplete)
            {
                await _lifecycleService.ResumeAsync(subscription.ItemId, cancellationToken);
            }
        }
    }
}
