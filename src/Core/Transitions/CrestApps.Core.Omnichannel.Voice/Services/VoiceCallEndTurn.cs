namespace CrestApps.Core.Omnichannel.Voice.Services;

/// <summary>
/// The scoped <see cref="IVoiceCallEndTurn"/>: one instance per call, holding whatever the end-call tool
/// recorded while that call was live.
/// </summary>
public sealed class VoiceCallEndTurn : IVoiceCallEndTurn
{
    private CancellationTokenSource _requested = new();

    /// <inheritdoc/>
    public bool EndCallRequested { get; private set; }

    /// <inheritdoc/>
    public string Reason { get; private set; }

    /// <inheritdoc/>
    public CancellationToken EndCallRequestedToken => _requested.Token;

    /// <inheritdoc/>
    public void RequestEndCall(string reason)
    {
        EndCallRequested = true;
        Reason = reason;

        // Signalled last, so the session woken by it already sees the decision it is reacting to.
        _requested.Cancel();
    }

    /// <inheritdoc/>
    public void Reset()
    {
        EndCallRequested = false;
        Reason = null;

        // A cancelled source cannot be reused, so the next call gets a fresh one. Without this a scope that ended
        // one call would read every later call as finished the instant anything observed the token.
        var previous = _requested;
        _requested = new CancellationTokenSource();
        previous.Dispose();
    }
}
