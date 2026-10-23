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
    /// <param name="configure">Sets what decides what is being bought, such as <see cref="CheckoutSession.References"/>, before the handlers run.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <exception cref="CheckoutRejectedException">A handler refused what the checkout pays for (<see cref="CheckoutFlowActivatingContext.Reject(string)"/>).</exception>
    Task<CheckoutSession> NewAsync(string referenceType, string referenceId, string referenceVersionId = null, Action<CheckoutSession> configure = null, CancellationToken cancellationToken = default);

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
    /// Returns every session started for the supplied reference, oldest first, whatever their status or owner. Unlike
    /// <see cref="GetByReferenceAsync"/>, which returns only the newest, this reaches the earlier attempts too, so a
    /// privacy handler can remove a person from all of them. This does not enforce ownership and does not run the
    /// checkout handlers, so the sessions must not be used to resume a customer-facing flow.
    /// </summary>
    /// <param name="referenceType">The reference type, for example <see cref="CheckoutReferenceTypes.Order"/>.</param>
    /// <param name="referenceId">The identifier of the referenced thing (for an order, its item id).</param>
    /// <param name="skip">The number of matching sessions to skip.</param>
    /// <param name="take">The maximum number of sessions to return.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching sessions.</returns>
    Task<IReadOnlyList<CheckoutSession>> ListByReferenceAsync(string referenceType, string referenceId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the sessions that belong to a user or whose buyer gave an email, oldest first, whatever their status.
    /// It is how a person's checkouts are found for an access or erasure request, including the guest checkouts only
    /// their email ties to them. The email is matched without case sensitivity. When both are given, a session that
    /// matches either is returned. This does not enforce ownership and does not run the checkout handlers, so the
    /// sessions must not be used to resume a customer-facing flow.
    /// </summary>
    /// <param name="ownerId">The owning user id, or <see langword="null"/> to match by email only.</param>
    /// <param name="email">The buyer's email, or <see langword="null"/> to match by owner only.</param>
    /// <param name="skip">The number of matching sessions to skip.</param>
    /// <param name="take">The maximum number of sessions to return.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The matching sessions, or none when neither an owner nor an email is given.</returns>
    Task<IReadOnlyList<CheckoutSession>> GetByCustomerAsync(string ownerId, string email, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the sessions with the given ids, whatever their status or owner. This does not enforce ownership and does
    /// not run the checkout handlers, so the sessions must not be used to resume a customer-facing flow.
    /// </summary>
    /// <param name="sessionIds">The session identifiers.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The sessions that exist; an unknown id is skipped.</returns>
    Task<IReadOnlyList<CheckoutSession>> GetManyAsync(IEnumerable<string> sessionIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to the session. New sessions created by <see cref="NewAsync"/> are untracked until
    /// this method is called, so it must be invoked to persist them; for sessions loaded through the query
    /// methods the call also makes the intent to persist explicit on the money-sensitive checkout path.
    /// </summary>
    /// <param name="session">The session to save.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <summary>
    /// Gets checkouts in one status that have not changed since a given moment, oldest first.
    /// </summary>
    /// <param name="status">The status to look for.</param>
    /// <param name="modifiedBeforeUtc">Only checkouts last modified before this moment are returned.</param>
    /// <param name="take">The maximum number to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stale checkouts.</returns>
    /// <remarks>
    /// This is how the sweep finds a checkout that is stuck: one waiting on a provider that never answered,
    /// one whose fulfillment failed after its payment was confirmed, or one a customer simply walked away
    /// from. None of those is reachable through a payment attempt alone.
    /// </remarks>
    Task<IReadOnlyList<CheckoutSession>> GetStaleAsync(CheckoutSessionStatus status, DateTime modifiedBeforeUtc, int take, CancellationToken cancellationToken = default);

    Task SaveAsync(CheckoutSession session, CancellationToken cancellationToken = default);
}
