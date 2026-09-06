namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The scoped <see cref="IOmnichannelHandoffTurn"/>: one instance per scope, holding whatever the transfer tool
/// recorded during that scope's completion.
/// </summary>
public sealed class OmnichannelHandoffTurn : IOmnichannelHandoffTurn
{
    private CancellationTokenSource _requested = new();

    /// <inheritdoc/>
    public bool HandoffRequested { get; private set; }

    /// <inheritdoc/>
    public string Reason { get; private set; }

    /// <inheritdoc/>
    public CancellationToken HandoffRequestedToken => _requested.Token;

    /// <inheritdoc/>
    public void RequestHandoff(string reason)
    {
        HandoffRequested = true;
        Reason = reason;

        // Signalled last, so anything woken by it already sees the decision it is reacting to.
        _requested.Cancel();
    }

    /// <inheritdoc/>
    public void Reset()
    {
        HandoffRequested = false;
        Reason = null;

        // A cancelled source cannot be reused, so the next turn gets a fresh one. Without this a scope that
        // handed off once would report every later turn as a handoff the instant anything observed the token.
        var previous = _requested;
        _requested = new CancellationTokenSource();
        previous.Dispose();
    }
}
