namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Persists and retrieves <see cref="CheckoutSession"/> instances. Implementations enforce session
/// ownership so an anonymous session cannot be hijacked by another visitor.
/// </summary>
public interface ICheckoutSessionStore
{
    /// <summary>
    /// Returns the session with the given id, regardless of status or owner.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task<CheckoutSession> GetAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the session with the given id and status, but only when it belongs to the current caller.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="status">The required session status.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task<CheckoutSession> GetAsync(string sessionId, CheckoutSessionStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and initializes a new checkout session for the given reference, running the registered
    /// checkout handlers so features can contribute their steps and billing items.
    /// </summary>
    /// <param name="referenceType">The kind of thing being purchased.</param>
    /// <param name="referenceId">The identifier of the thing being purchased.</param>
    /// <param name="referenceVersionId">An optional secondary identifier of the thing being purchased.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task<CheckoutSession> NewAsync(string referenceType, string referenceId, string referenceVersionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recently created session that matches the supplied reference. The order owns the
    /// authoritative reverse link by storing its checkout session id, so this lookup is a recovery and
    /// reconciliation path (for example when that stored id was lost, or when correlating an out-of-band
    /// provider event) rather than the primary way an order finds its session. When
    /// <paramref name="referenceVersionId"/> is supplied it is included in the match; otherwise sessions
    /// are matched on reference type and id only. This does not enforce ownership, so callers that resume a
    /// customer-facing flow must still authorize the caller against the returned session.
    /// </summary>
    /// <param name="referenceType">The reference type, for example <see cref="CheckoutReferenceTypes.Order"/>.</param>
    /// <param name="referenceId">The identifier of the referenced thing (for an order, its item id).</param>
    /// <param name="referenceVersionId">The optional draft or quote version identifier to match.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task<CheckoutSession> GetByReferenceAsync(string referenceType, string referenceId, string referenceVersionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to the session. New sessions created by <see cref="NewAsync"/> are untracked until
    /// this method is called, so it must be invoked to persist them; for sessions loaded through the query
    /// methods the call also makes the intent to persist explicit on the money-sensitive checkout path.
    /// </summary>
    /// <param name="session">The session to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    Task SaveAsync(CheckoutSession session, CancellationToken cancellationToken = default);
}
