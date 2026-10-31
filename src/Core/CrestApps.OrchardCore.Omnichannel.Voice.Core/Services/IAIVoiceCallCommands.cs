namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Reaches the live session holding an AI call on this node.
/// </summary>
/// <remarks>
/// A realtime session holds its call inside one request on one node for the whole call, so a supervisor's request
/// has to find that session where it runs. The session registers its handlers when it starts and removes them when
/// it ends; a request for a call no session on this node holds is reported as not delivered, and the caller falls
/// back to what works without the session (a turn-based call's next turn, or the provider itself).
/// </remarks>
public interface IAIVoiceCallCommands
{
    /// <summary>
    /// Registers a handler for a call's commands until the returned registration is disposed.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="handler">Handles a command, returning <see langword="true"/> when it acted on it.</param>
    /// <returns>The registration.</returns>
    IDisposable Register(string activityId, Func<AIVoiceCallCommand, Task<bool>> handler);

    /// <summary>
    /// Sends a command to the session holding a call on this node.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="command">The command.</param>
    /// <returns><see langword="true"/> when a session acted on it.</returns>
    Task<bool> SendAsync(string activityId, AIVoiceCallCommand command);

    /// <summary>
    /// Sends a command to the live session holding a call on any of the tenant's nodes: this node first, then the
    /// others through the relay when one is registered (with Redis). Use it only for a call a live session holds
    /// (<c>AILiveCall.IsRealtime</c>), since a call nobody holds waits for the relay to give up.
    /// </summary>
    /// <param name="activityId">The call's activity.</param>
    /// <param name="command">The command.</param>
    /// <returns><see langword="true"/> when a session acted on it.</returns>
    Task<bool> RelayAsync(string activityId, AIVoiceCallCommand command);
}
