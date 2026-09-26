namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// The most likely reason a call rated poor, read from what was measured on it.
/// </summary>
public enum CallQualityCause
{
    /// <summary>
    /// Nothing measured points at one cause.
    /// </summary>
    Unknown,

    /// <summary>
    /// Packets were sent but no audio arrived: a media path that never connected, or one a firewall blocked.
    /// </summary>
    NoAudioReceived,

    /// <summary>
    /// Audio was lost on the way: usually a congested or wireless network.
    /// </summary>
    PacketLoss,

    /// <summary>
    /// Audio arrived unevenly: usually a congested or wireless network.
    /// </summary>
    Jitter,

    /// <summary>
    /// Audio took too long to arrive: usually a long or relayed network path.
    /// </summary>
    Latency,

    /// <summary>
    /// The agent's side was good and the customer's side was not: the customer's phone or carrier.
    /// </summary>
    CustomerSide,

    /// <summary>
    /// The agent's audio never left: the caller could not hear the agent, usually a microphone track that had stopped.
    /// </summary>
    NoAudioSent,
}
