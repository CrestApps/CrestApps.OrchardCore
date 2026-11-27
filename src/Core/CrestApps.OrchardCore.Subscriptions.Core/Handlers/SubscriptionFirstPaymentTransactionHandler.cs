using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Starts a subscription that was bought with a promise to pay (Pay Later, a bank transfer) once its first transaction
/// is paid, and brings one that fell past due back once nothing of its checkout is overdue. Until its first payment it
/// waits as <see cref="SubscriptionStatus.Incomplete"/> and grants nothing.
/// </summary>
/// <remarks>
/// One rule with <see cref="SubscriptionOverdueTransactionHandler"/>: a subscription is past due while any transaction of
/// the checkout that created it is overdue and unpaid. That handler marks every subscription of the checkout when one of
/// its transactions falls overdue, a sign-up fee or a one-time item included, so any paid transaction of the checkout may
/// bring them back, once none of its transactions is overdue and unpaid.
/// </remarks>
public sealed class SubscriptionFirstPaymentTransactionHandler : TransactionEventHandlerBase
{
    // A checkout holds a handful of transactions; the pages are only there so one with years of renewals is still read.
    private const int PageSize = 100;

    private readonly ISubscriptionLifecycleService _lifecycleService;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ITransactionStore _transactionStore;
    private readonly ICheckoutPaymentProviderResolver _providerResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFirstPaymentTransactionHandler"/> class.
    /// </summary>
    /// <param name="lifecycleService">The lifecycle service, which starts and resumes subscriptions.</param>
    /// <param name="subscriptionManager">The subscriptions.</param>
    /// <param name="transactionStore">The transactions, read to find transactions of the checkout that are still overdue.</param>
    /// <param name="providerResolver">The payment providers, which tell an agreement billed here from one a gateway bills.</param>
    public SubscriptionFirstPaymentTransactionHandler(
        ISubscriptionLifecycleService lifecycleService,
        ISubscriptionManager subscriptionManager,
        ITransactionStore transactionStore,
        ICheckoutPaymentProviderResolver providerResolver)
    {
        _lifecycleService = lifecycleService;
        _subscriptionManager = subscriptionManager;
        _transactionStore = transactionStore;
        _providerResolver = providerResolver;
    }

    /// <inheritdoc/>
    public override async Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
    {
        if (current != TransactionStatus.Paid || string.IsNullOrEmpty(transaction?.CheckoutSessionId))
        {
            return;
        }

        bool? othersOverdue = null;

        foreach (var subscription in await _subscriptionManager.GetByCheckoutSessionAsync(transaction.CheckoutSessionId, cancellationToken))
        {
            // Pay Later records one transaction per obligation of the checkout, so a plan with a sign-up fee, or a cart with a
            // one-time item next to the plan, leaves more than one. Only the transaction for the subscription's own obligation
            // pays for its first cycle; paying the one-time balance, or a later cycle, must not start the plan.
            if (subscription.Status == SubscriptionStatus.Incomplete && PaysFor(transaction, subscription))
            {
                var started = await _lifecycleService.ResumeAsync(subscription.ItemId, cancellationToken);

                await AlignFirstCycleAsync(transaction, started, cancellationToken);

                continue;
            }

            // An overdue transaction of the checkout made the agreement past due: an invoice of the agreement, its sign-up fee
            // or a one-time item bought with it. Paying one brings the agreement back once nothing of the checkout is overdue;
            // otherwise the grace window ran out on a customer who had paid, or a cycle paid over an unpaid fee cleared it.
            // An agreement a payment provider bills is past due when the provider's renewal failed, which no transaction here
            // records; only the provider's payment brings it back. Resuming it here gave back what it grants, a sold site
            // included, while the provider's renewal was still unpaid.
            // Pay Later records a reference for every agreement it takes, so the reference alone does not make one
            // gateway-billed; the provider's capability decides.
            if (subscription.Status == SubscriptionStatus.PastDue &&
                OfflineSubscriptionBilling.IsOffline(subscription, _providerResolver) &&
                !(othersOverdue ??= await HasOtherOverdueAsync(transaction, cancellationToken)))
            {
                await _lifecycleService.ResumeAsync(subscription.ItemId, cancellationToken);
            }
        }
    }

    // The subscription's first period starts when its first payment arrives. The transaction that paid for it carries the
    // period the renewal sweep invoices the next cycle from, so it moves with it; otherwise a buyer who paid late was
    // invoiced the next cycle at once, for a period the subscription had not reached.
    private async Task AlignFirstCycleAsync(Transaction paid, Subscription started, CancellationToken cancellationToken)
    {
        if (started is not { Status: SubscriptionStatus.Active } ||
            paid.Recurrence is not { CycleNumber: 1, NextCycleCreated: false } recurrence ||
            (recurrence.PeriodStartUtc == started.CurrentPeriodStartUtc && recurrence.PeriodEndUtc == started.CurrentPeriodEndUtc))
        {
            return;
        }

        recurrence.PeriodStartUtc = started.CurrentPeriodStartUtc;
        recurrence.PeriodEndUtc = started.CurrentPeriodEndUtc;

        await _transactionStore.UpdateAsync(paid, cancellationToken);
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

    // Whether any other transaction of the checkout is overdue and unpaid.
    private async Task<bool> HasOtherOverdueAsync(Transaction paid, CancellationToken cancellationToken)
    {
        var query = new TransactionQuery { CheckoutSessionId = paid.CheckoutSessionId, OutstandingOnly = true };

        for (var page = 1; ; page++)
        {
            var result = await _transactionStore.PageAsync(page, PageSize, query, cancellationToken);
            var entries = result?.Entries ?? [];

            if (entries.Any(candidate =>
                !string.Equals(candidate.ItemId, paid.ItemId, StringComparison.Ordinal) &&
                candidate.BecameOverdueUtc is not null &&
                candidate.Status is TransactionStatus.Outstanding or TransactionStatus.PartiallyPaid))
            {
                return true;
            }

            if (entries.Count < PageSize)
            {
                return false;
            }
        }
    }
}
