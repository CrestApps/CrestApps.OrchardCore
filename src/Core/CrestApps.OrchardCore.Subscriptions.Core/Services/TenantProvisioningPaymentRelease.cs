using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Releases a site bought with a promise to pay once what it was bought for is paid: the transaction of the site's
/// obligation is paid, or the installment plan that replaced it received every payment. The payment handler runs it
/// when a transaction is paid, and staff can run it again from the provisioning list.
/// </summary>
public sealed class TenantProvisioningPaymentRelease
{
    private const string EndedSubscriptionError = "The subscription this site was bought with ended before the site was built, so it was not created.";

    private readonly ITenantProvisioningJobStore _jobStore;
    private readonly ITransactionStore _transactionStore;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IInstallmentPlanStore _planStore;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningPaymentRelease"/> class.
    /// </summary>
    /// <param name="jobStore">The provisioning job store.</param>
    /// <param name="transactionStore">The transaction store.</param>
    /// <param name="subscriptionManager">The subscription manager.</param>
    /// <param name="planStores">The installment plan store, when the Installment Plans feature is on.</param>
    /// <param name="clock">The clock.</param>
    public TenantProvisioningPaymentRelease(
        ITenantProvisioningJobStore jobStore,
        ITransactionStore transactionStore,
        ISubscriptionManager subscriptionManager,
        IEnumerable<IInstallmentPlanStore> planStores,
        IClock clock)
    {
        _jobStore = jobStore;
        _transactionStore = transactionStore;
        _subscriptionManager = subscriptionManager;
        _planStore = planStores.FirstOrDefault();
        _clock = clock;
    }

    /// <summary>
    /// Releases the site a transaction that was just paid pays for: directly, or as the last payment of an installment
    /// plan that replaced the site's transaction.
    /// </summary>
    /// <param name="paid">The transaction that was just paid.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the job was checked.</returns>
    public async Task ReleaseForPaymentAsync(Transaction paid, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paid);

        if (!string.IsNullOrEmpty(paid.CheckoutSessionId))
        {
            await TryReleaseAsync(await _jobStore.GetByCheckoutSessionAsync(paid.CheckoutSessionId, cancellationToken), paid, cancellationToken);
        }

        // An installment payment carries no checkout of the site; the plan it belongs to names the transaction it replaced.
        if (_planStore is null ||
            !string.Equals(paid.ReferenceType, SubscriptionConstants.InstallmentPlans.ReferenceType, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(paid.ReferenceId) ||
            await _planStore.FindByIdAsync(paid.ReferenceId, cancellationToken) is not { } plan ||
            string.IsNullOrEmpty(plan.SourceTransactionId) ||
            await _transactionStore.FindByIdAsync(plan.SourceTransactionId, cancellationToken) is not { } source ||
            string.IsNullOrEmpty(source.CheckoutSessionId))
        {
            return;
        }

        await TryReleaseAsync(await _jobStore.GetByCheckoutSessionAsync(source.CheckoutSessionId, cancellationToken), paid, cancellationToken);
    }

    /// <summary>
    /// Queues a job that waits for its payment, when what the site was bought for is paid.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the job was queued.</returns>
    public Task<bool> TryReleaseAsync(TenantProvisioningJob job, CancellationToken cancellationToken = default)
        => TryReleaseAsync(job, justPaid: null, cancellationToken);

    /// <summary>
    /// Gives up the site a subscription's checkout is waiting to be paid for, once that subscription has ended. Its name,
    /// prefix and hosts are held for it until then, and a site built after its subscription ended would run with nothing
    /// left to suspend it.
    /// </summary>
    /// <param name="subscription">The subscription that changed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when a waiting job was given up.</returns>
    public async Task<bool> AbandonEndedAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        if (string.IsNullOrEmpty(subscription.CheckoutSessionId) ||
            await _jobStore.GetByCheckoutSessionAsync(subscription.CheckoutSessionId, cancellationToken) is not { Status: TenantProvisioningStatus.AwaitingPayment } job)
        {
            return false;
        }

        // The subscription that changed is read as it is now, whatever the store still returns for it.
        var subscriptions = (await _subscriptionManager.GetByCheckoutSessionAsync(subscription.CheckoutSessionId, cancellationToken))
            .Select(candidate => string.Equals(candidate.ItemId, subscription.ItemId, StringComparison.Ordinal) ? subscription : candidate)
            .ToArray();

        if (!HaveEnded(subscriptions, _clock.UtcNow))
        {
            return false;
        }

        await AbandonAsync(job, cancellationToken);

        return true;
    }

    /// <summary>
    /// Whether every subscription a site was bought with has ended: expired, or canceled with no paid time left. A site
    /// bought once, with no subscription, never has.
    /// </summary>
    /// <param name="subscriptions">The subscriptions of the checkout the site was bought with.</param>
    /// <param name="utcNow">The current time.</param>
    internal static bool HaveEnded(IReadOnlyCollection<Subscription> subscriptions, DateTime utcNow)
        => subscriptions is { Count: > 0 } &&
            subscriptions.All(subscription => subscription.Status == SubscriptionStatus.Expired ||
                (subscription.Status == SubscriptionStatus.Canceled && !subscription.IsCurrent(utcNow)));

    private async Task<bool> TryReleaseAsync(TenantProvisioningJob job, Transaction justPaid, CancellationToken cancellationToken)
    {
        if (job is null ||
            job.Status != TenantProvisioningStatus.AwaitingPayment ||
            string.IsNullOrEmpty(job.CheckoutSessionId))
        {
            return false;
        }

        var subscriptions = await _subscriptionManager.GetByCheckoutSessionAsync(job.CheckoutSessionId, cancellationToken);

        // A late payment for a subscription that already ended built a site that nothing suspended afterwards: the
        // transition that ended the subscription found no site to suspend, and none follows it.
        if (HaveEnded(subscriptions, _clock.UtcNow))
        {
            await AbandonAsync(job, cancellationToken);

            return false;
        }

        if (!await IsPaidForAsync(job.CheckoutSessionId, subscriptions, justPaid, cancellationToken))
        {
            return false;
        }

        job.Status = TenantProvisioningStatus.Pending;
        job.NextAttemptUtc = null;
        job.UpdatedUtc = _clock.UtcNow;

        await _jobStore.UpdateAsync(job, cancellationToken);

        return true;
    }

    /// <summary>
    /// Gives up a job whose site's subscription ended before the site was built: it is never built, and the secret it kept
    /// for the site's administrator is dropped.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <param name="utcNow">The current time.</param>
    internal static void MarkSubscriptionEnded(TenantProvisioningJob job, DateTime utcNow)
    {
        job.Status = TenantProvisioningStatus.Abandoned;
        job.NextAttemptUtc = null;
        job.ProtectedAdminPassword = null;
        job.LastError = EndedSubscriptionError;
        job.UpdatedUtc = utcNow;
    }

    private async Task AbandonAsync(TenantProvisioningJob job, CancellationToken cancellationToken)
    {
        MarkSubscriptionEnded(job, _clock.UtcNow);

        await _jobStore.UpdateAsync(job, cancellationToken);
    }

    // The site's obligation is paid: its transaction is, or the installment plan that replaced it got every payment. The
    // transaction being paid right now counts as paid, since the store and the plan may be updated after it.
    private async Task<bool> IsPaidForAsync(string checkoutSessionId, IReadOnlyCollection<Subscription> subscriptions, Transaction justPaid, CancellationToken cancellationToken)
    {
        if (justPaid is not null &&
            string.Equals(justPaid.CheckoutSessionId, checkoutSessionId, StringComparison.Ordinal) &&
            PaysForSite(justPaid, subscriptions))
        {
            return true;
        }

        foreach (var transaction in await _transactionStore.GetByCheckoutSessionAsync(checkoutSessionId, cancellationToken))
        {
            if (!PaysForSite(transaction, subscriptions))
            {
                continue;
            }

            if (transaction.Status == TransactionStatus.Paid)
            {
                return true;
            }

            if (transaction.Status == TransactionStatus.Canceled && _planStore is not null)
            {
                var plans = await _planStore.GetBySourceTransactionAsync(transaction.ItemId, cancellationToken);

                if (plans.Any(plan => plan.Status == InstallmentPlanStatus.Completed ||
                    (plan.Status is InstallmentPlanStatus.Active or InstallmentPlanStatus.PastDue &&
                        plan.Payments.Count > 0 &&
                        plan.Payments.All(payment => payment.Status == InstallmentPaymentStatus.Paid ||
                            (justPaid is not null && string.Equals(payment.TransactionId, justPaid.ItemId, StringComparison.Ordinal))))))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Pay Later records one transaction per obligation of the checkout. A site sold with a plan is paid for by the plan's
    // own obligation, not by a sign-up fee or a small item next to it; a site sold once, with no plan, by the one-time
    // obligation.
    private static bool PaysForSite(Transaction transaction, IReadOnlyCollection<Subscription> subscriptions)
        => subscriptions.Count > 0
            ? subscriptions.Any(subscription => SubscriptionFirstPaymentTransactionHandler.PaysFor(transaction, subscription))
            : string.Equals(transaction.ObligationId, CheckoutObligations.OneTime, StringComparison.Ordinal);
}
