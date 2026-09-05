namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The scoped <see cref="IOmnichannelHandoffTurn"/>: one instance per scope, holding whatever the transfer tool
/// recorded during that scope's completion.
/// </summary>
public sealed class OmnichannelHandoffTurn : IOmnichannelHandoffTurn
{
    /// <inheritdoc/>
    public bool HandoffRequested { get; private set; }

    /// <inheritdoc/>
    public string Reason { get; private set; }

    /// <inheritdoc/>
    public void RequestHandoff(string reason)
    {
        HandoffRequested = true;
        Reason = reason;
    }

    /// <inheritdoc/>
    public void Reset()
    {
        HandoffRequested = false;
        Reason = null;
    }
}
