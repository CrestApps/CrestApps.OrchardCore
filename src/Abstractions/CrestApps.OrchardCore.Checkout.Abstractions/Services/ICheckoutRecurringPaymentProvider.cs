using System.Threading;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// An optional, additive capability a payment provider implements to establish and manage a recurring
/// billing agreement, alongside the one-time charging it already provides through
/// <see cref="ICheckoutPaymentProvider"/>.
/// </summary>
/// <remarks>
/// It is a separate interface for the same reason refunds are: it makes
/// <see cref="PaymentProviderCapabilities.SupportsRecurringPayments"/> an executable promise rather than an
/// unimplemented flag, and it leaves a provider that only charges once (or only records an offline
/// commitment) untouched.
///
/// Establishing a recurring agreement is genuinely different from taking a payment. A gateway typically has
/// to store a reusable payment method against a customer before it can create the agreement, and the first
/// cycle may then need its own confirmation. That is why the begin context carries the provider data the
/// client collected up front, and why the result can still ask the customer to act.
/// </remarks>
public interface ICheckoutRecurringPaymentProvider
{
    /// <summary>
    /// The stable key of the provider this capability belongs to. It matches the owning
    /// <see cref="ICheckoutPaymentProvider.Key"/>.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Establishes the recurring agreement for one billing interval and returns the provider's authoritative
    /// reference so the caller can persist it before anything else happens.
    /// </summary>
    /// <remarks>
    /// Implementations must be idempotent on <see cref="Models.PaymentAttempt.IdempotencyKey"/>, so a retried
    /// begin resumes the same agreement instead of creating a second one and billing the customer twice.
    /// </remarks>
    /// <param name="context">The recurring begin context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PaymentBeginResult> BeginRecurringAsync(BeginRecurringPaymentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an established recurring agreement, either immediately or at the end of the period the
    /// customer has already paid for.
    /// </summary>
    /// <param name="context">The cancellation context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<RecurringCancelResult> CancelRecurringAsync(CancelRecurringPaymentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Suspends collection on an established agreement without ending it, and resumes it again.
    /// </summary>
    /// <remarks>
    /// Pausing only in this application would tell the operator that billing is suspended while the gateway
    /// keeps charging the customer every cycle. A provider that has no notion of pausing should cancel
    /// nothing and report failure, so the caller can say so rather than imply a suspension that never
    /// happened.
    /// </remarks>
    /// <param name="context">The pause context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<RecurringPauseResult> PauseRecurringAsync(PauseRecurringPaymentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes what an established agreement bills, for example when a customer moves to another plan or
    /// changes quantity.
    /// </summary>
    /// <param name="context">The update context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<RecurringUpdateResult> UpdateRecurringAsync(UpdateRecurringPaymentContext context, CancellationToken cancellationToken = default);
}
