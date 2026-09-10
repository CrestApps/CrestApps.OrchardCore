namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Observes real-time voice events for module-originated agent legs and releases the connect operation that is
/// waiting for the agent channel to enter the Stasis application. The agent leg's <c>StasisStart</c> is the
/// first moment the channel can be bridged, so this bridge signals readiness for it and claims the event: it is
/// internal call-control orchestration and must not be projected onto a caller interaction.
/// </summary>
internal sealed class AsteriskAgentChannelReadyBridge : IAsteriskRealtimeVoiceEventBridge
{
    private readonly IAsteriskAgentChannelReadySignal _agentChannelReadySignal;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskAgentChannelReadyBridge"/> class.
    /// </summary>
    /// <param name="agentChannelReadySignal">The tenant-scoped agent channel readiness signal.</param>
    public AsteriskAgentChannelReadyBridge(IAsteriskAgentChannelReadySignal agentChannelReadySignal)
    {
        _agentChannelReadySignal = agentChannelReadySignal;
    }

    /// <summary>
    /// Signals readiness for a module-originated agent leg's <c>StasisStart</c> event.
    /// </summary>
    /// <param name="voiceEvent">The normalized Asterisk voice event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the event was an owned-origination <c>StasisStart</c>; otherwise, <see langword="false"/>.</returns>
    public Task<bool> TryHandleAsync(
        AsteriskRealtimeVoiceEvent voiceEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voiceEvent);

        if (!voiceEvent.IsOwnedOrigination ||
            string.IsNullOrWhiteSpace(voiceEvent.ChannelId) ||
            !string.Equals(voiceEvent.EventType, "StasisStart", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        _agentChannelReadySignal.Signal(voiceEvent.ChannelId);

        return Task.FromResult(true);
    }
}
