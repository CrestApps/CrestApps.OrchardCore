using System.Threading;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Orchestrates a checkout from start to settlement. It is the single entry point for every money-moving
/// step: callers start a checkout, begin payment, and try to complete it through this service, and never
/// call a payment provider, the attempt ledger, or the reconciliation service directly.
/// </summary>
/// <remarks>
/// Centralizing the orchestration is what makes the safety guarantees real rather than conventions each
/// caller has to remember. The engine is responsible for persisting a durable attempt <em>before</em> the
/// provider is contacted, storing the provider's reference the moment it is returned, refusing to complete a
/// checkout until every obligation is confirmed against the provider's own API, and compensating the
/// obligations that did succeed when a sibling obligation fails. A caller that reimplemented this would
/// eventually get one of those steps wrong, and the failure mode is a customer charged for something the
/// site does not record.
///
/// Every operation is safe to call concurrently and repeatedly. Completion in particular is driven from
/// three unrelated directions — the customer's browser, a provider webhook, and a background sweep — so it
/// serializes on the session and is a no-op once the session reaches a terminal state.
/// </remarks>
public interface ICheckoutEngine
{
    /// <summary>
    /// Creates a checkout for the supplied reference, runs the registered handlers so features contribute
    /// their steps and billing items, builds the authoritative invoice, and persists the session.
    /// </summary>
    /// <param name="request">The description of what is being purchased.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The persisted, pending session.</returns>
    Task<CheckoutSession> StartAsync(StartCheckoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins payment for every obligation the session's invoice expects, using the requested provider.
    /// </summary>
    /// <remarks>
    /// Each obligation gets its own durable attempt, written before the provider is called, so a partial
    /// failure is always attributable and compensatable. Calling this again for a session that already has
    /// attempts resumes them instead of creating a second set, so a customer who refreshes the payment page
    /// or double-submits is never charged twice.
    /// </remarks>
    /// <param name="sessionId">The checkout session id.</param>
    /// <param name="options">The provider selection and redirect URLs.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>What the client needs to finish the payment with the provider.</returns>
    Task<PaymentBeginOutcome> BeginPaymentAsync(string sessionId, BeginPaymentOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to settle and complete the checkout by verifying every outstanding obligation against its
    /// provider's authoritative API.
    /// </summary>
    /// <remarks>
    /// This never blocks waiting for a provider to make up its mind. When an obligation is still pending the
    /// session is left awaiting settlement and the caller is told to try again later, so the same call can be
    /// driven by a polling client, a webhook, and the reconciliation sweep without any of them holding a
    /// request open. Completion handlers run exactly once, guarded by the status transition committed under
    /// the session lock.
    /// </remarks>
    /// <param name="sessionId">The checkout session id.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The outcome of the attempt to complete.</returns>
    Task<CheckoutCompletionResult> TryCompleteAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a checkout the customer abandoned, releasing any remote resources its attempts created.
    /// </summary>
    /// <param name="sessionId">The checkout session id.</param>
    /// <param name="reason">The reason recorded for audit.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task<CheckoutCompletionResult> CancelAsync(string sessionId, string reason, CancellationToken cancellationToken = default);
}
