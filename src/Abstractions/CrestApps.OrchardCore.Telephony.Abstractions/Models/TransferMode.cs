namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes how an active call should be transferred to another destination.
/// </summary>
public enum TransferMode
{
    /// <summary>
    /// The call is transferred immediately without the agent speaking to the destination first.
    /// </summary>
    Blind = 0,

    /// <summary>
    /// The agent speaks to the destination before completing the transfer (consultative/warm transfer).
    /// </summary>
    Warm = 1,
}
