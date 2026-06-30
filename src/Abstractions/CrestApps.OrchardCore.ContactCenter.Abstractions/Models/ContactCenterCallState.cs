namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the normalized, provider-neutral state of a Contact Center voice call session. Providers
/// report their own call state, which is normalized into these values so routing, analytics, and the
/// agent/supervisor UX can reason about calls independently of any specific provider.
/// </summary>
public enum ContactCenterCallState
{
    /// <summary>
    /// The call has been planned (for example reserved for an outbound dial) but not yet placed.
    /// </summary>
    Planned,

    /// <summary>
    /// An outbound call is being placed and is awaiting connection.
    /// </summary>
    Dialing,

    /// <summary>
    /// The call is alerting and waiting for the remote party or agent to answer.
    /// </summary>
    Ringing,

    /// <summary>
    /// The call is connected and media is flowing.
    /// </summary>
    Connected,

    /// <summary>
    /// The call is connected but currently on hold.
    /// </summary>
    OnHold,

    /// <summary>
    /// The call is in the process of ending.
    /// </summary>
    Ending,

    /// <summary>
    /// The call ended normally.
    /// </summary>
    Ended,

    /// <summary>
    /// The call failed due to an error or provider failure.
    /// </summary>
    Failed,

    /// <summary>
    /// The outbound call was not answered.
    /// </summary>
    NoAnswer,

    /// <summary>
    /// The call was rejected by the remote party or agent.
    /// </summary>
    Rejected,

    /// <summary>
    /// The call was canceled before it connected.
    /// </summary>
    Canceled,

    /// <summary>
    /// The call was transferred to another destination.
    /// </summary>
    Transferred,
}
