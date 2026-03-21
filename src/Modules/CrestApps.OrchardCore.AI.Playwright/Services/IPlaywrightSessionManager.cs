using CrestApps.OrchardCore.AI.Playwright.Models;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

/// <summary>
/// Manages Playwright browser sessions keyed by chat session ID.
/// Implementations are registered as singletons.
/// </summary>
public interface IPlaywrightSessionManager
{
    /// <summary>
    /// Returns the existing session for the request, or creates one using the supplied
    /// browser mode, base URLs, and optional credentials.
    /// </summary>
    Task<IPlaywrightSession> GetOrCreateAsync(
        PlaywrightSessionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the session for <paramref name="chatSessionId"/>, or <see langword="null"/> if none exists.
    /// </summary>
    IPlaywrightSession GetSession(string chatSessionId, string ownerId = null);

    /// <summary>
    /// Navigates to Orchard admin and attempts authentication if the current page is at the login screen
    /// and the session has credentials available.
    /// </summary>
    Task EnsureAdminReadyAsync(IPlaywrightSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the current in-flight operation for the session. Browser stays open.
    /// </summary>
    void Stop(string chatSessionId, string ownerId = null);

    /// <summary>
    /// Disposes the session and closes the browser window.
    /// </summary>
    Task CloseAsync(string chatSessionId, string ownerId = null);

    /// <summary>
    /// Closes any Playwright sessions that have been idle past their configured timeout.
    /// </summary>
    Task CloseInactiveSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all active (non-closed) sessions keyed by chat session ID.
    /// </summary>
    IReadOnlyDictionary<string, IPlaywrightSession> GetActiveSessions(string ownerId = null);
}
