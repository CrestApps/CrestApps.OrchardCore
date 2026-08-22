using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Persists and retrieves <see cref="SubscriptionSession"/> instances. Implementations enforce session
/// ownership so an anonymous subscription session cannot be resumed by a different visitor.
/// </summary>
public interface ISubscriptionSessionStore
{
    /// <summary>
    /// Returns the subscription session with the given id, regardless of status or owner.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The matching session, or <see langword="null"/> when none exists.</returns>
    Task<SubscriptionSession> GetAsync(string sessionId);

    /// <summary>
    /// Returns the subscription session with the given id and status, but only when it belongs to the
    /// current caller.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="status">The required session status.</param>
    /// <returns>The matching session, or <see langword="null"/> when none exists or ownership fails.</returns>
    Task<SubscriptionSession> GetAsync(string sessionId, SubscriptionSessionStatus status);

    /// <summary>
    /// Creates and initializes a new subscription session for the given subscription content item.
    /// </summary>
    /// <param name="subscriptionContentItem">The subscription content item being purchased.</param>
    /// <returns>The newly created session.</returns>
    Task<SubscriptionSession> NewAsync(ContentItem subscriptionContentItem);

    /// <summary>
    /// Persists changes to the subscription session. A new session created by <see cref="NewAsync"/> is
    /// untracked until this method is called, so it must be invoked to persist it.
    /// </summary>
    /// <param name="session">The session to save.</param>
    Task SaveAsync(SubscriptionSession session);
}
