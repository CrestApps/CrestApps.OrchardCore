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
    Task<CheckoutSession> GetAsync(string sessionId);

    /// <summary>
    /// Returns the session with the given id and status, but only when it belongs to the current caller.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="status">The required session status.</param>
    Task<CheckoutSession> GetAsync(string sessionId, CheckoutSessionStatus status);

    /// <summary>
    /// Creates and initializes a new checkout session for the given reference, running the registered
    /// checkout handlers so features can contribute their steps and billing items.
    /// </summary>
    /// <param name="referenceType">The kind of thing being purchased.</param>
    /// <param name="referenceId">The identifier of the thing being purchased.</param>
    /// <param name="referenceVersionId">An optional secondary identifier of the thing being purchased.</param>
    Task<CheckoutSession> NewAsync(string referenceType, string referenceId, string referenceVersionId = null);

    /// <summary>
    /// Persists changes to the session.
    /// </summary>
    /// <param name="session">The session to save.</param>
    Task SaveAsync(CheckoutSession session);
}
