namespace CrestApps.OrchardCore.AI.Core.Conversations;

/// <summary>
/// Closes an AI conversation as soon as it is known to be over, such as when a call hangs up, instead of waiting for
/// the profile's inactivity timeout. Closing runs the same steps the inactivity close does: the post-session
/// analysis, the contact memory and the <c>AI Chat Session Closed</c> workflow event.
/// </summary>
public interface IAIChatSessionCloser
{
    /// <summary>
    /// Closes a conversation that is still active.
    /// </summary>
    /// <param name="sessionId">The AI chat session.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the conversation was closed; <see langword="false"/> when there is no such
    /// session, it was already closed, or its profile is gone.</returns>
    Task<bool> CloseAsync(string sessionId, CancellationToken cancellationToken = default);
}
