using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Starts a subscription that was bought with a promise to pay (Pay Later, a bank transfer) once its first transaction
/// is paid, and brings one that fell past due back once its overdue invoices are paid. Until its first payment it waits
/// as <see cref="SubscriptionStatus.Incomplete"/> and grants nothing.
/// </summary>
public sealed class SubscriptionFirstPaymentTransactionHandler : TransactionEventHandlerBase
{
    private const string CycleSeparator = ":cycle-";

    // A monthly agreement invoices twelve cycles a year; the walk below stops long before this.
    private const int MaxCycles = 1000;

    private readonly ISubscriptionLifecycleService _lifecycleService;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ITransactionStore _transactionStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFirstPaymentTransactionHandler"/> class.
    /// </summary>
    /// <param name="lifecycleService">The lifecycle service, which starts and resumes subscriptions.</param>
    /// <param name="subscriptionManager">The subscriptions.</param>
    /// <param name="transactionStore">The transactions, read to find invoices of the agreement that are still overdue.</param>
    public SubscriptionFirstPaymentTransactionHandler(
        ISubscriptionLifecycleService lifecycleService,
        ISubscriptionManager subscriptionManager,
        ITransactionStore transactionStore)
    {
        _lifecycleService = lifecycleService;
        _subscriptionManager = subscriptionManager;
        _transactionStore = transactionStore;
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

                continue;
            }

            // An overdue invoice made the agreement past due. Paying it brings the agreement back, as long as no other
            // invoice of it is still overdue; otherwise the grace window ran out on a customer who had paid.
            if (subscription.Status == SubscriptionStatus.PastDue &&
                Bills(transaction, subscription) &&
                !await HasOtherOverdueAsync(subscription, transaction, cancellationToken))
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

    // The first cycle carries the subscription's own obligation; each renewal appends ":cycle-<n>" to the one before it.
    private static bool Bills(Transaction transaction, Subscription subscription)
        => PaysFor(transaction, subscription) ||
            (!string.IsNullOrEmpty(subscription.ObligationId) &&
                transaction.ObligationId?.StartsWith(subscription.ObligationId + CycleSeparator, StringComparison.Ordinal) == true);

    // Walks the agreement's invoices the way Pay Later chains them, from the first cycle to the newest.
    private async Task<bool> HasOtherOverdueAsync(Subscription subscription, Transaction paid, CancellationToken cancellationToken)
    {
        var obligationId = subscription.ObligationId;

        for (var cycle = 0; cycle < MaxCycles && !string.IsNullOrEmpty(obligationId); cycle++)
        {
            var invoice = await _transactionStore.GetByObligationAsync(subscription.CheckoutSessionId, obligationId, cancellationToken);

            if (invoice is null)
            {
                return false;
            }

            if (!string.Equals(invoice.ItemId, paid.ItemId, StringComparison.Ordinal) &&
                invoice.BecameOverdueUtc is not null &&
                invoice.Status is TransactionStatus.Outstanding or TransactionStatus.PartiallyPaid)
            {
                return true;
            }

            obligationId = invoice.Recurrence is { } recurrence
                ? $"{invoice.ObligationId}{CycleSeparator}{recurrence.CycleNumber + 1}"
                : null;
        }

        return false;
    }
}
