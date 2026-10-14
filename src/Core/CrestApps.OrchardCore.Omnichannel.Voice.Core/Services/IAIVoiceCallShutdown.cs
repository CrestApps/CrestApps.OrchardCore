namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Hands this node's live AI voice calls on before the node stops, so a restart does not leave their callers on a
/// silent line.
/// </summary>
/// <remarks>
/// A realtime conversation runs inside this process, holding the call's media stream for as long as the call
/// lasts. When the process went, the conversation went with it, but the call stayed up at the carrier with nobody
/// speaking on it, and the caller listened to silence until they hung up. When the host begins to stop, every live
/// conversation is now ended as a lost session, which hands its caller to a person when the call has one to reach,
/// and otherwise apologizes and ends the call.
/// </remarks>
public interface IAIVoiceCallShutdown
{
    /// <summary>
    /// Gets the token that fires when this node begins to shut down. A conversation holding a call ends itself
    /// when it fires, and one that starts after it has fired ends at once.
    /// </summary>
    CancellationToken Stopping { get; }

    /// <summary>
    /// Counts a live call as held by this node until the returned handle is disposed, so the node waits for it to
    /// be handed on before it stops.
    /// </summary>
    /// <returns>The handle that releases the call.</returns>
    IDisposable HoldCall();

    /// <summary>
    /// Keeps the node from stopping, for a short while, until the work that hands a call on has finished.
    /// </summary>
    /// <param name="work">The work that finishes the call.</param>
    void TrackHandOn(Task work);
}
