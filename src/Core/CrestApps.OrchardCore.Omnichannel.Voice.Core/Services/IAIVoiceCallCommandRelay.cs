namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Carries a command for a live AI call to the other nodes of a tenant, when the node that received it does not hold
/// the call.
/// </summary>
/// <remarks>
/// A live session runs in memory on the node that answered the call. Without a relay, a supervisor whose request
/// lands on another node cannot reach it; with one, the command is offered to every node and the node holding the
/// call answers whether it carried it out.
/// </remarks>
public interface IAIVoiceCallCommandRelay
{
    /// <summary>
    /// Starts listening for commands sent by other nodes. Calling it again does nothing.
    /// </summary>
    /// <param name="deliverLocally">
    /// Hands a command to this node's live calls. Returns <see langword="null"/> when no call with that activity
    /// runs here, and otherwise whether it was carried out.
    /// </param>
    /// <returns>A task that completes when the relay listens.</returns>
    Task StartAsync(Func<string, AIVoiceCallCommand, Task<bool?>> deliverLocally);

    /// <summary>
    /// Offers a command to the other nodes and waits for the node holding the call to answer.
    /// </summary>
    /// <param name="activityId">The call's activity identifier.</param>
    /// <param name="command">The command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when another node carried out the command.</returns>
    Task<bool> SendAsync(string activityId, AIVoiceCallCommand command, CancellationToken cancellationToken = default);
}
