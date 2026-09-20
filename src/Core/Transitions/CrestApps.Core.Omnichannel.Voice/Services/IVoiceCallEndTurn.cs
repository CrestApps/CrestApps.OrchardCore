namespace CrestApps.Core.Omnichannel.Voice.Services;

/// <summary>
/// Carries the model's decision that an automated call is over from the end-call tool back to the session
/// holding the line.
/// </summary>
/// <remarks>
/// A turn-based call ends itself: the model writes a hangup marker in the reply it is about to speak, and the
/// call is dropped once that line finishes. A realtime call has no such moment — the model is speaking, not
/// returning text — so without this it says goodbye and then holds the line open until the person on the other
/// end gives up and hangs up on it. That is the part callers describe as the machine not knowing the
/// conversation is over.
/// <para>
/// Scoped, like <see cref="CrestApps.Core.Omnichannel.Services.IOmnichannelHandoffTurn"/>: the tool
/// and the session resolve the same instance within one call, and two calls running at once cannot see each
/// other's decision.
/// </para>
/// </remarks>
public interface IVoiceCallEndTurn
{
    /// <summary>
    /// Gets a value indicating whether the model has said the conversation is finished.
    /// </summary>
    bool EndCallRequested { get; }

    /// <summary>
    /// Gets the reason the model gave for ending the call.
    /// </summary>
    string Reason { get; }

    /// <summary>
    /// Gets a token that is cancelled the moment the model says the conversation is finished.
    /// </summary>
    /// <remarks>
    /// The session watches this rather than polling a flag, because it is busy carrying audio in both directions
    /// and the decision arrives from a tool invocation on another thread.
    /// </remarks>
    CancellationToken EndCallRequestedToken { get; }

    /// <summary>
    /// Records that the model considers the conversation complete.
    /// </summary>
    /// <param name="reason">The reason the model gave.</param>
    void RequestEndCall(string reason);

    /// <summary>
    /// Clears the decision, so the call that follows starts from nothing recorded.
    /// </summary>
    void Reset();
}
