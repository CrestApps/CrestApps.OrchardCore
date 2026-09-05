namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Carries the current automated conversation turn's handoff decision from the <c>transfer_to_agent</c> tool
/// back to the conversation handler that ran the AI completion. The handler opens a scope for the turn, runs the
/// completion (which auto-invokes the tool when the model decides to escalate), then reads
/// <see cref="OmnichannelHandoffTurn.HandoffRequested"/> to perform the channel-specific handoff.
/// </summary>
/// <remarks>
/// Backed by <see cref="AsyncLocal{T}"/> so the value flows into the completion's tool invocation regardless of
/// dependency-injection scoping. The handler sets a fresh turn object before the completion and clears it after;
/// the tool mutates that same object, so the handler sees the decision on the same async flow.
/// </remarks>
public static class OmnichannelHandoffTurnContext
{
    private static readonly AsyncLocal<OmnichannelHandoffTurn> _current = new();

    /// <summary>
    /// Gets the handoff turn for the current async flow, or <see langword="null"/> when no turn is active.
    /// </summary>
    public static OmnichannelHandoffTurn Current => _current.Value;

    /// <summary>
    /// Begins a handoff turn for the current async flow. Dispose the returned scope to end it.
    /// </summary>
    /// <returns>A scope that clears the turn on dispose, and exposes the turn it created.</returns>
    public static Scope Begin()
    {
        var turn = new OmnichannelHandoffTurn();
        _current.Value = turn;

        return new Scope(turn);
    }

    /// <summary>
    /// Records a handoff request on the current turn, if one is active. Called by the tool.
    /// </summary>
    /// <param name="reason">The reason the model gave for escalating.</param>
    /// <returns><see langword="true"/> when a turn was active and the request was recorded.</returns>
    public static bool RequestHandoff(string reason)
    {
        var turn = _current.Value;

        if (turn is null)
        {
            return false;
        }

        turn.HandoffRequested = true;
        turn.Reason = reason;

        return true;
    }

    /// <summary>
    /// A handoff turn scope. Exposes the turn and clears the ambient value on dispose.
    /// </summary>
    public readonly struct Scope : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Scope"/> struct.
        /// </summary>
        /// <param name="turn">The turn this scope owns.</param>
        public Scope(OmnichannelHandoffTurn turn)
        {
            Turn = turn;
        }

        /// <summary>
        /// Gets the handoff turn this scope created.
        /// </summary>
        public OmnichannelHandoffTurn Turn { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (ReferenceEquals(_current.Value, Turn))
            {
                _current.Value = null;
            }
        }
    }
}

/// <summary>
/// The mutable state of one automated conversation turn's handoff decision.
/// </summary>
public sealed class OmnichannelHandoffTurn
{
    /// <summary>
    /// Gets or sets a value indicating whether the model invoked the transfer tool this turn.
    /// </summary>
    public bool HandoffRequested { get; set; }

    /// <summary>
    /// Gets or sets the reason the model gave for escalating.
    /// </summary>
    public string Reason { get; set; }
}
