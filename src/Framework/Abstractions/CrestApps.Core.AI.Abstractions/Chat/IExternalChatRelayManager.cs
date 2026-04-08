using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Chat;

/// <summary>
/// Manages the lifecycle of <see cref="IExternalChatRelay"/> instances, tracking active
/// relay connections by session ID. This service is registered as a singleton so that
/// relay connections persist across scoped service lifetimes.
/// </summary>
/// <remarks>
/// <para>
/// Use this manager to retrieve an existing relay for a session or create a new one.
/// When a session ends or the relay is no longer needed, call <see cref="CloseAsync"/>
/// to gracefully disconnect and dispose of the relay.
/// </para>
/// </remarks>
public interface IExternalChatRelayManager
{
    /// <summary>
    /// Gets an existing relay for the specified session, or creates and connects a new one
    /// using the provided factory function.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="context">The relay context for establishing a new connection.</param>
    /// <param name="factory">
    /// A factory function that creates a new <see cref="IExternalChatRelay"/> instance
    /// when no existing relay is found for the session.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The active relay instance for the session.</returns>
    Task<IExternalChatRelay> GetOrCreateAsync(
        string sessionId,
        ExternalChatRelayContext context,
        Func<IExternalChatRelay> factory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an existing relay for the specified session, or <see langword="null"/> if none exists.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The active relay instance, or <see langword="null"/>.</returns>
    IExternalChatRelay Get(string sessionId);

    /// <summary>
    /// Gracefully disconnects and removes the relay for the specified session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task CloseAsync(string sessionId, CancellationToken cancellationToken = default);
}
