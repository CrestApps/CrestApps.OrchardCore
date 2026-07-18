namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Coordinates the point at which a module-originated agent channel has entered the Stasis application and
/// can be bridged. It is a tenant singleton so every waiter and signal is isolated to the current tenant and
/// never observed by another tenant. Instances hold only per-tenant, in-memory registration state.
/// </summary>
internal interface IAsteriskAgentChannelReadySignal
{
    /// <summary>
    /// Registers interest in the readiness of the supplied agent channel before it is originated. The returned
    /// registration must be disposed once the connect attempt completes so the pending waiter is released.
    /// </summary>
    /// <param name="channelId">The deterministic identifier of the agent channel being originated.</param>
    /// <returns>A registration whose wait completes when the channel becomes ready, times out, or is cancelled.</returns>
    IAsteriskAgentChannelReadyRegistration Register(string channelId);

    /// <summary>
    /// Signals that the supplied agent channel has entered the Stasis application and is ready to be bridged.
    /// </summary>
    /// <param name="channelId">The identifier of the agent channel that became ready.</param>
    void Signal(string channelId);
}
