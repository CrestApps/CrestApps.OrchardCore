namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes the final outcome of a telephony interaction, used for history and reporting.
/// </summary>
public enum CallOutcome
{
    /// <summary>
    /// The call is currently active.
    /// </summary>
    InProgress = 0,

    /// <summary>
    /// The call connected and completed normally.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// An inbound call was not answered.
    /// </summary>
    Missed = 2,

    /// <summary>
    /// An inbound call was rejected by the user.
    /// </summary>
    Rejected = 3,

    /// <summary>
    /// The call failed to connect or was terminated because of an error.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// An outbound call was canceled before it connected.
    /// </summary>
    Canceled = 5,
}
