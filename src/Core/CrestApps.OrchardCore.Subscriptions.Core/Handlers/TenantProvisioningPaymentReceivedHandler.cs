using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Releases a site that was bought with a promise to pay once the transaction that pays for it is paid: the provisioning
/// job waiting for the payment becomes due.
/// </summary>
public sealed class TenantProvisioningPaymentReceivedHandler : TransactionEventHandlerBase
{
    private readonly ITenantProvisioningJobStore _jobStore;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningPaymentReceivedHandler"/> class.
    /// </summary>
    public TenantProvisioningPaymentReceivedHandler(ITenantProvisioningJobStore jobStore, ISubscriptionManager subscriptionManager, IClock clock)
    {
        _jobStore = jobStore;
        _subscriptionManager = subscriptionManager;
        _clock = clock;
    }

    /// <inheritdoc/>
    public override async Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
    {
        if (current != TransactionStatus.Paid || string.IsNullOrEmpty(transaction?.CheckoutSessionId))
        {
            return;
        }

        var job = await _jobStore.GetByCheckoutSessionAsync(transaction.CheckoutSessionId, cancellationToken);

        if (job is null || job.Status != TenantProvisioningStatus.AwaitingPayment || !await PaysForSiteAsync(transaction, cancellationToken))
        {
            return;
        }

        job.Status = TenantProvisioningStatus.Pending;
        job.NextAttemptUtc = null;
        job.UpdatedUtc = _clock.UtcNow;

        await _jobStore.UpdateAsync(job, cancellationToken);
    }

    // Pay Later records one transaction per obligation of the checkout. A site sold with a plan is paid for by the plan's
    // own obligation, not by a sign-up fee or a small item next to it; a site sold once, with no plan, by the one-time
    // obligation.
    private async Task<bool> PaysForSiteAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        var subscriptions = await _subscriptionManager.GetByCheckoutSessionAsync(transaction.CheckoutSessionId, cancellationToken);

        return subscriptions.Count > 0
            ? subscriptions.Any(subscription => SubscriptionFirstPaymentTransactionHandler.PaysFor(transaction, subscription))
            : string.Equals(transaction.ObligationId, CheckoutObligations.OneTime, StringComparison.Ordinal);
    }
}
