namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies how a Contact Center voice provider delivers a live call to the selected agent. The
/// orchestration layer must branch on this value to decide whether it has to bridge media to the
/// agent itself or whether the provider already rings the agent's device.
/// </summary>
public enum VoiceProviderDeliveryModel
{
    /// <summary>
    /// The provider rings the agent's own registered device or soft-phone client (for example WebRTC).
    /// The live call already reaches the agent, so the Contact Center reserves, offers, and tracks the
    /// work, but the agent answers the media on their device and the platform does not bridge it.
    /// </summary>
    AgentDeviceNative,

    /// <summary>
    /// The provider parks or queues the live call server-side. The Contact Center must explicitly ask
    /// the provider to connect (bridge) the call to the selected agent once the offer is accepted.
    /// </summary>
    ServerSideAcd,
}
