namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies Contact Center orchestration capabilities supported by a voice provider.
/// </summary>
[Flags]
public enum ContactCenterVoiceProviderCapabilities
{
    /// <summary>
    /// The provider does not expose Contact Center-specific voice operations.
    /// </summary>
    None = 0,

    /// <summary>
    /// The provider can place outbound calls on behalf of the dialer.
    /// </summary>
    DialerDial = 1 << 0,

    /// <summary>
    /// The provider can assign an existing call to an agent.
    /// </summary>
    AgentCallAssignment = 1 << 1,

    /// <summary>
    /// The provider can place or move calls into provider-side queues.
    /// </summary>
    ProviderQueue = 1 << 2,

    /// <summary>
    /// The provider can report provider queue events to Contact Center.
    /// </summary>
    QueueEvents = 1 << 3,

    /// <summary>
    /// The provider can synchronize agent availability or PBX presence with Contact Center.
    /// </summary>
    AgentPresenceSync = 1 << 4,

    /// <summary>
    /// The provider can connect (bridge) a live call to a selected agent. Required for providers whose
    /// delivery model is <see cref="VoiceProviderDeliveryModel.ServerSideAcd"/>.
    /// </summary>
    AgentConnect = 1 << 5,

    /// <summary>
    /// The provider can transfer a live call to another agent, queue, or external destination.
    /// </summary>
    CallTransfer = 1 << 6,

    /// <summary>
    /// The provider can add participants to a live call (conference).
    /// </summary>
    Conference = 1 << 7,
}
