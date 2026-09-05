namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Keeps one automated reply in flight per conversation. A newer inbound message makes the reply being composed
/// stale, so beginning a generation cancels the one it supersedes and takes its place.
/// </summary>
/// <remarks>
/// This replaces a process-wide static registry. As a registered service it is per tenant, so one tenant's
/// conversation can no longer cancel another tenant's generation that happens to share a session id, and a test
/// can drive it without reaching into static state.
/// </remarks>
public interface IAutomatedConversationGate
{
    /// <summary>
    /// Begins a generation for the conversation, cancelling and replacing any generation already running for it.
    /// </summary>
    /// <param name="sessionId">The AI chat session the conversation is held in.</param>
    /// <param name="hostToken">The caller's own token, which the returned token also honours.</param>
    /// <returns>
    /// A registration exposing the token this generation must run under. Disposing it deregisters this
    /// generation, unless a newer one has already replaced it.
    /// </returns>
    IAutomatedConversationGeneration Begin(string sessionId, CancellationToken hostToken);

    /// <summary>
    /// Determines whether a reply is being composed for the conversation right now on this node.
    /// </summary>
    /// <param name="sessionId">The AI chat session the conversation is held in.</param>
    /// <returns><see langword="true"/> when a generation is in flight.</returns>
    bool IsGenerating(string sessionId);
}
