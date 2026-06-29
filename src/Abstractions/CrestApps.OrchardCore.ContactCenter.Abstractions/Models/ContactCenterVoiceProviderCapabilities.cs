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
}
