namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Controls how long an automated conversation waits before sending each AI reply, so responses do not feel
/// instant and robotic. Chosen when the automated inventory is loaded and snapshotted onto each activity.
/// </summary>
public enum OmnichannelResponseDelayMode
{
    /// <summary>
    /// No artificial delay; replies are sent as soon as they are generated.
    /// </summary>
    None,

    /// <summary>
    /// Wait a fixed number of seconds before every reply.
    /// </summary>
    Fixed,

    /// <summary>
    /// Wait a base number of seconds randomized by a jitter, so each reply waits a slightly different amount.
    /// </summary>
    Random,
}
