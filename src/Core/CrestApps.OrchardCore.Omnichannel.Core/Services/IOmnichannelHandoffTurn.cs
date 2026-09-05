namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Carries one automated conversation turn's handoff decision from the transfer-to-agent tool back to the
/// conversation handler that ran the AI completion. The handler resets it before the completion, the tool
/// records the model's decision on it during the completion, and the handler reads it afterwards.
/// </summary>
/// <remarks>
/// This is a scoped service rather than ambient static state. A completion runs inside one scope, so the tool
/// and the handler resolve the same instance; making it a service also means two conversations processed
/// concurrently in the same process cannot see each other's decision, and a test can assert the decision without
/// reaching into a process-wide slot.
/// </remarks>
public interface IOmnichannelHandoffTurn
{
    /// <summary>
    /// Gets a value indicating whether the model invoked the transfer tool this turn.
    /// </summary>
    bool HandoffRequested { get; }

    /// <summary>
    /// Gets the reason the model gave for escalating.
    /// </summary>
    string Reason { get; }

    /// <summary>
    /// Records that the model asked to hand the conversation to a live agent.
    /// </summary>
    /// <param name="reason">The reason the model gave.</param>
    void RequestHandoff(string reason);

    /// <summary>
    /// Clears the decision, so the turn that follows starts from nothing recorded.
    /// </summary>
    void Reset();
}
