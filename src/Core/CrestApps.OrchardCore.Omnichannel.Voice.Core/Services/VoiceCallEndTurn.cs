namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The scoped <see cref="IVoiceCallEndTurn"/>: one instance per call, holding whatever the end-call tool
/// recorded while that call was live.
/// </summary>
public sealed class VoiceCallEndTurn : IVoiceCallEndTurn
{
    private CancellationTokenSource _requested = new();

    private int _requestCount;

    /// <inheritdoc/>
    public bool EndCallRequested { get; private set; }

    /// <inheritdoc/>
    public string Reason { get; private set; }

    /// <inheritdoc/>
    public bool ReachedVoicemail { get; private set; }

    /// <inheritdoc/>
    public CancellationToken EndCallRequestedToken => _requested.Token;

    public int RequestCount => Volatile.Read(ref _requestCount);

    /// <inheritdoc/>
    public void RequestEndCall(string reason)
        => RequestEndCall(reason, reachedVoicemail: false);

    /// <inheritdoc/>
    public void RequestEndCall(string reason, bool reachedVoicemail)
    {
        EndCallRequested = true;
        Reason = reason;
        ReachedVoicemail = reachedVoicemail;
        Interlocked.Increment(ref _requestCount);

        // Signalled last, so the session woken by it already sees the decision it is reacting to.
        _requested.Cancel();
    }

    /// <inheritdoc/>
    public void Reset()
    {
        EndCallRequested = false;
        Reason = null;
        ReachedVoicemail = false;
        Volatile.Write(ref _requestCount, 0);

        // A cancelled source cannot be reused, so the next call gets a fresh one. Without this a scope that ended
        // one call would read every later call as finished the instant anything observed the token.
        var previous = _requested;
        _requested = new CancellationTokenSource();
        previous.Dispose();
    }
}
