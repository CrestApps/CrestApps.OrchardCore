namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes the lifecycle state of a telephony call.
/// </summary>
public enum CallState
{
    /// <summary>
    /// No active call.
    /// </summary>
    Idle = 0,

    /// <summary>
    /// An outbound call is being placed and is awaiting connection.
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// The remote party is being alerted (ringing).
    /// </summary>
    Ringing = 2,

    /// <summary>
    /// The call is connected and media is flowing.
    /// </summary>
    Connected = 3,

    /// <summary>
    /// The call is connected but placed on hold.
    /// </summary>
    OnHold = 4,

    /// <summary>
    /// The call has ended.
    /// </summary>
    Disconnected = 5,

    /// <summary>
    /// The call failed to connect or was terminated because of an error.
    /// </summary>
    Failed = 6,
}
