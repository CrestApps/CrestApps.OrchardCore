namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The scoped <see cref="IVoiceCallEndTurn"/>: one instance per call, holding whatever the end-call tool
/// recorded while that call was live.
/// </summary>
public sealed class VoiceCallEndTurn : IVoiceCallEndTurn
{
    private CancellationTokenSource _requested = new();

    private int _requestCount;
    private int _awaitingAnswer;
    private int _holds;
    private int _customerSpokeLast;
    private int _lastLineSaidGoodbye;

    // How many times one unanswered question may keep the call open against the model's request to end it.
    private const int MaximumHolds = 2;

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
    /// <inheritdoc/>
    public void AssistantSaid(string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            Volatile.Write(ref _customerSpokeLast, 0);
            Volatile.Write(ref _lastLineSaidGoodbye, VoiceGoodbye.SoundsLikeOne(line) ? 1 : 0);
        }

        // Only set here, never cleared: a goodbye said straight after the question, without the customer
        // answering, is exactly what must not close the call.
        if (VoiceConfirmation.AwaitsAnswer(line))
        {
            Volatile.Write(ref _awaitingAnswer, 1);
            Volatile.Write(ref _holds, 0);
        }
    }

    /// <inheritdoc/>
    public void CustomerAnswered()
    {
        Volatile.Write(ref _awaitingAnswer, 0);
        Volatile.Write(ref _customerSpokeLast, 1);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Owed too when the assistant spoke last but not to say goodbye: live, a model said "let me wrap this up for you",
    /// ended the call, was told to say nothing further, and the line dropped with no goodbye at all.
    /// </remarks>
    public bool ClosingLineOwed
        => Volatile.Read(ref _customerSpokeLast) == 1 || Volatile.Read(ref _lastLineSaidGoodbye) == 0;

    /// <inheritdoc/>
    public bool TryHoldForAnswer()
    {
        if (Volatile.Read(ref _awaitingAnswer) == 0)
        {
            return false;
        }

        return Interlocked.Increment(ref _holds) <= MaximumHolds;
    }

    public void Reset()
    {
        Volatile.Write(ref _awaitingAnswer, 0);
        Volatile.Write(ref _holds, 0);
        Volatile.Write(ref _customerSpokeLast, 0);
        Volatile.Write(ref _lastLineSaidGoodbye, 0);
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
