using System.Threading;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// A first-class checkout payment provider. Providers (Stripe, Pay Later, ...) implement this to begin a
/// payment, verify it against their authoritative API, and cancel/compensate a remote resource. The
/// checkout framework drives these three operations against a durable <see cref="Models.PaymentAttempt"/>
/// so a charge is never lost or double-applied, even across distributed nodes.
/// </summary>
public interface ICheckoutPaymentProvider
{
    /// <summary>
    /// The stable, unique key that identifies this provider (for example the Stripe processor key).
    /// </summary>
    string Key { get; }

    /// <summary>
    /// The localized display title for the provider, shown when the customer selects a payment method.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// What the provider can do, so the checkout can select and constrain it correctly.
    /// </summary>
    PaymentProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Begins a payment for the supplied attempt. Implementations must be idempotent on
    /// <see cref="Models.PaymentAttempt.IdempotencyKey"/> and must return the provider's authoritative
    /// reference so the caller can persist it on the attempt before doing anything else.
    /// </summary>
    /// <param name="context">The begin-payment context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies an attempt against the provider's authoritative API and reports what really happened. This
    /// is the source of truth used at completion; a cached webhook notification is only a hint.
    /// </summary>
    /// <param name="context">The verify-payment context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels or compensates the remote resource created for an attempt that is being abandoned or rolled
    /// back (for example after a later obligation in the same checkout failed). The returned result is only
    /// treated as compensated when the provider confirms it, so a failed void or refund is never silently
    /// assumed to have succeeded.
    /// </summary>
    /// <param name="context">The cancel-payment context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default);
}
