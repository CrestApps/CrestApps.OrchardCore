using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// The single place a <see cref="Subscription"/> changes state.
/// </summary>
/// <remarks>
/// Subscription state is edited from several directions at once: a provider webhook, a nightly sweep, an
/// operator in the admin, and the customer in their portal. If each wrote the record itself they would
/// disagree, and the disagreements are expensive. A renewal applied twice bills twice; a cancellation racing
/// a renewal either bills someone who left or gives away a cycle. So every transition goes through here,
/// each one takes a lock on the subscription, each one is idempotent, and each one writes an event.
/// </remarks>
public interface ISubscriptionLifecycleService
{
    /// <summary>
    /// Records that a billing cycle was paid and moves the agreement to the next period.
    /// </summary>
    /// <remarks>
    /// It is idempotent on <paramref name="periodStartUtc"/>: a webhook and the renewal sweep both reporting
    /// the same cycle advance the subscription once.
    /// </remarks>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="periodStartUtc">The UTC start of the period that was paid for.</param>
    /// <param name="context">How the renewal was learned about and what it settled.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Subscription> RecordRenewalAsync(string subscriptionId, DateTime periodStartUtc, SubscriptionRenewalContext context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a renewal payment failed and starts the dunning window.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="reason">The reason the payment failed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Subscription> MarkPastDueAsync(string subscriptionId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the agreement, either at the end of the paid period or immediately.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="atPeriodEnd">Whether the agreement runs to the end of the period the customer paid for.</param>
    /// <param name="reason">The reason recorded for the cancellation.</param>
    /// <param name="source">Who or what canceled it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Subscription> CancelAsync(string subscriptionId, bool atPeriodEnd, string reason, string source = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a past-due agreement to active, for example once a failed payment is retried successfully.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Subscription> ResumeAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends billing without ending the agreement.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="reason">The reason recorded.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Subscription> PauseAsync(string subscriptionId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the agreement because it ran its agreed number of cycles or its grace period elapsed.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="reason">The reason recorded.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Subscription> ExpireAsync(string subscriptionId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the state a provider reports, so the local record follows the gateway that actually bills.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="context">What the provider reported.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<Subscription> SyncFromProviderAsync(string subscriptionId, SubscriptionProviderSyncContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a renewal settled, so the ledger and the agreement agree about a cycle.
/// </summary>
public sealed class SubscriptionRenewalContext
{
    /// <summary>
    /// Gets or sets the provider's transaction identifier for the cycle.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the gross amount collected, including tax. When null the subscription's own amount is
    /// used.
    /// </summary>
    public decimal? AmountPaid { get; set; }

    /// <summary>
    /// Gets or sets the UTC end of the period the payment covers, when the provider reports one. Trusting
    /// the provider's period beats recomputing it, because the provider is what actually bills next.
    /// </summary>
    public DateTime? PeriodEndUtc { get; set; }

    /// <summary>
    /// Gets or sets who reported the renewal.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a durable payment attempt should be written for the cycle, so
    /// revenue reports read one ledger rather than reconstructing subscription revenue separately.
    /// </summary>
    public bool RecordPaymentAttempt { get; set; } = true;
}

/// <summary>
/// The state a payment provider reports for an agreement.
/// </summary>
public sealed class SubscriptionProviderSyncContext
{
    /// <summary>
    /// Gets or sets the status the provider reports, when it maps to one this application knows.
    /// </summary>
    public SubscriptionStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC end of the current period the provider reports.
    /// </summary>
    public DateTime? CurrentPeriodEndUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the provider says the agreement stops at the end of the paid period.
    /// </summary>
    public bool? CancelAtPeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets who reported the change.
    /// </summary>
    public string Source { get; set; }
}
